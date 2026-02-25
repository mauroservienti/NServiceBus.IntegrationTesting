using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using NServiceBus.IntegrationTesting.Proto;

namespace NServiceBus.IntegrationTesting.Containers;

public sealed record HandlerInvokedEvent(
    string EndpointName,
    string HandlerTypeName,
    string MessageTypeName,
    string CorrelationId,
    bool HasError,
    string ErrorMessage);

/// <summary>
/// gRPC service implementation that runs in the test host process.
/// Agents (in endpoint processes/containers) connect to this service.
/// </summary>
public sealed class TestHostGrpcService : TestHostService.TestHostServiceBase
{
    // One TaskCompletionSource per expected endpoint name.
    // Completes when the agent for that endpoint connects.
    readonly ConcurrentDictionary<string, TaskCompletionSource> _agentConnections = new();

    // Per-agent outbound channels. The background ForwardCommandsAsync task
    // drains each channel and writes to the agent's response stream.
    readonly ConcurrentDictionary<string, Channel<HostToAgentMessage>> _commandChannels = new();

    // Fan-out: each WaitForHandlerInvocationAsync call registers its own channel here.
    // Arriving events are written to every registered channel so concurrent waiters each
    // receive a copy and can filter independently by correlation ID and handler type name.
    readonly Lock _listenersLock = new();
    readonly List<Channel<HandlerInvokedEvent>> _listeners = [];

    /// <summary>
    /// Returns a Task that completes when the named endpoint's agent connects.
    /// </summary>
    public Task WaitForAgentAsync(string endpointName, CancellationToken cancellationToken = default)
    {
        var tcs = _agentConnections.GetOrAdd(
            endpointName,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    /// <summary>
    /// Returns a Task that completes when a HandlerInvokedEvent with the given
    /// correlation ID and handler type name is reported by any agent.
    /// Each call registers an independent listener — safe for concurrent tests.
    /// </summary>
    public async Task<HandlerInvokedEvent> WaitForHandlerInvocationAsync(
        string correlationId,
        string handlerTypeName,
        CancellationToken cancellationToken = default)
    {
        var myChannel = Channel.CreateUnbounded<HandlerInvokedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        lock (_listenersLock)
            _listeners.Add(myChannel);

        try
        {
            await foreach (var evt in myChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId && evt.HandlerTypeName == handlerTypeName)
                    return evt;
            }

            throw new OperationCanceledException(
                $"Channel completed without a '{handlerTypeName}' invocation for correlation '{correlationId}'.");
        }
        finally
        {
            lock (_listenersLock)
                _listeners.Remove(myChannel);
            myChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Instructs the named agent to execute a registered scenario by name.
    /// Returns the correlation ID that ties all HandlerInvokedEvents produced
    /// by this execution together — pass it to WaitForHandlerInvocationAsync.
    /// </summary>
    public async Task<string> ExecuteScenarioAsync(
        string endpointName,
        string scenarioName,
        Dictionary<string, string>? args = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        var channel = _commandChannels.GetOrAdd(
            endpointName,
            _ => Channel.CreateUnbounded<HostToAgentMessage>());

        var msg = new ExecuteScenarioMessage
        {
            ScenarioName = scenarioName,
            CorrelationId = correlationId
        };
        if (args is { Count: > 0 })
            foreach (var (k, v) in args)
                msg.Args[k] = v;

        await channel.Writer.WriteAsync(
            new HostToAgentMessage { ExecuteScenario = msg },
            cancellationToken);

        return correlationId;
    }

    public override async Task OpenChannel(
        IAsyncStreamReader<AgentToHostMessage> requestStream,
        IServerStreamWriter<HostToAgentMessage> responseStream,
        ServerCallContext context)
    {
        Channel<HostToAgentMessage>? commandChannel = null;
        Task? commandForwarder = null;

        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                switch (message.PayloadCase)
                {
                    case AgentToHostMessage.PayloadOneofCase.Connect:
                        var endpointName = message.Connect.EndpointName;

                        var tcs = _agentConnections.GetOrAdd(
                            endpointName,
                            _ => new TaskCompletionSource(
                                TaskCreationOptions.RunContinuationsAsynchronously));

                        tcs.TrySetResult();

                        // Write ReadyMessage before starting the forwarder — this is the
                        // last write from this loop; all subsequent writes are via the forwarder.
                        await responseStream.WriteAsync(
                            new HostToAgentMessage { Ready = new ReadyMessage() },
                            context.CancellationToken);

                        // Get or create the command channel for this endpoint, then start
                        // the background task that drains it into the response stream.
                        // After this point the foreach loop does NOT write to responseStream.
                        commandChannel = _commandChannels.GetOrAdd(
                            endpointName,
                            _ => Channel.CreateUnbounded<HostToAgentMessage>());

                        commandForwarder = ForwardCommandsAsync(
                            commandChannel, responseStream, context.CancellationToken);
                        break;

                    case AgentToHostMessage.PayloadOneofCase.HandlerInvoked:
                        var evt = message.HandlerInvoked;
                        var handlerEvent = new HandlerInvokedEvent(
                            evt.EndpointName,
                            evt.HandlerTypeName,
                            evt.MessageTypeName,
                            evt.CorrelationId,
                            evt.HasError,
                            evt.ErrorMessage);

                        // Fan out to every registered waiter — no event is consumed.
                        lock (_listenersLock)
                            foreach (var listener in _listeners)
                                listener.Writer.TryWrite(handlerEvent);
                        break;
                }
            }
        }
        finally
        {
            // Signal the forwarder that no more commands are coming, then wait for it.
            commandChannel?.Writer.TryComplete();
            if (commandForwarder is not null)
                await commandForwarder;
        }
    }

    static async Task ForwardCommandsAsync(
        Channel<HostToAgentMessage> channel,
        IServerStreamWriter<HostToAgentMessage> responseStream,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var command in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await responseStream.WriteAsync(command, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
    }
}
