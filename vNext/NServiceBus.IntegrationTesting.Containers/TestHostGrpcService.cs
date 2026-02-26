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
    string ErrorMessage,
    bool IsSaga,
    bool SagaNotFound,
    string SagaTypeName,
    string SagaId,
    bool SagaIsNew,
    bool SagaIsCompleted);

public sealed record MessageDispatchedEvent(
    string EndpointName,
    string MessageTypeName,
    string Intent,
    string CorrelationId,
    bool HasError,
    string ErrorMessage);

public sealed record MessageFailedEvent(
    string EndpointName,
    string MessageTypeName,
    string ExceptionMessage,
    string CorrelationId);

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

    // Fan-out listener lists — each WaitFor call registers its own channel and
    // receives a copy of every event so concurrent tests can filter independently.
    readonly Lock _handlerListenersLock = new();
    readonly List<Channel<HandlerInvokedEvent>> _handlerListeners = [];

    readonly Lock _dispatchedListenersLock = new();
    readonly List<Channel<MessageDispatchedEvent>> _dispatchedListeners = [];

    readonly Lock _failedListenersLock = new();
    readonly List<Channel<MessageFailedEvent>> _failedListeners = [];

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

        lock (_handlerListenersLock)
            _handlerListeners.Add(myChannel);

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
            lock (_handlerListenersLock)
                _handlerListeners.Remove(myChannel);
            myChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Returns a Task that completes when a MessageDispatchedEvent with the given
    /// correlation ID and message type name is reported by any agent.
    /// </summary>
    public async Task<MessageDispatchedEvent> WaitForMessageDispatchedAsync(
        string correlationId,
        string messageTypeName,
        CancellationToken cancellationToken = default)
    {
        var myChannel = Channel.CreateUnbounded<MessageDispatchedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        lock (_dispatchedListenersLock)
            _dispatchedListeners.Add(myChannel);

        try
        {
            await foreach (var evt in myChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId && evt.MessageTypeName == messageTypeName)
                    return evt;
            }

            throw new OperationCanceledException(
                $"Channel completed without a '{messageTypeName}' dispatch for correlation '{correlationId}'.");
        }
        finally
        {
            lock (_dispatchedListenersLock)
                _dispatchedListeners.Remove(myChannel);
            myChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Returns a Task that completes when a MessageFailedEvent with the given
    /// correlation ID is reported by any agent.
    /// </summary>
    public async Task<MessageFailedEvent> WaitForMessageFailureAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var myChannel = Channel.CreateUnbounded<MessageFailedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        lock (_failedListenersLock)
            _failedListeners.Add(myChannel);

        try
        {
            await foreach (var evt in myChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId)
                    return evt;
            }

            throw new OperationCanceledException(
                $"Channel completed without a failure event for correlation '{correlationId}'.");
        }
        finally
        {
            lock (_failedListenersLock)
                _failedListeners.Remove(myChannel);
            myChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Instructs the named agent to execute a registered scenario by name.
    /// Returns the correlation ID that ties all events produced by this execution
    /// together — pass it to WaitForHandlerInvocationAsync / WaitForMessageDispatchedAsync.
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
                        var handlerEvt = message.HandlerInvoked;
                        var handlerEvent = new HandlerInvokedEvent(
                            handlerEvt.EndpointName,
                            handlerEvt.HandlerTypeName,
                            handlerEvt.MessageTypeName,
                            handlerEvt.CorrelationId,
                            handlerEvt.HasError,
                            handlerEvt.ErrorMessage,
                            handlerEvt.IsSaga,
                            handlerEvt.SagaNotFound,
                            handlerEvt.SagaTypeName,
                            handlerEvt.SagaId,
                            handlerEvt.SagaIsNew,
                            handlerEvt.SagaIsCompleted);

                        lock (_handlerListenersLock)
                            foreach (var listener in _handlerListeners)
                                listener.Writer.TryWrite(handlerEvent);
                        break;

                    case AgentToHostMessage.PayloadOneofCase.MessageDispatched:
                        var dispatchedEvt = message.MessageDispatched;
                        var dispatchedEvent = new MessageDispatchedEvent(
                            dispatchedEvt.EndpointName,
                            dispatchedEvt.MessageTypeName,
                            dispatchedEvt.Intent,
                            dispatchedEvt.CorrelationId,
                            dispatchedEvt.HasError,
                            dispatchedEvt.ErrorMessage);

                        lock (_dispatchedListenersLock)
                            foreach (var listener in _dispatchedListeners)
                                listener.Writer.TryWrite(dispatchedEvent);
                        break;

                    case AgentToHostMessage.PayloadOneofCase.MessageFailed:
                        var failedEvt = message.MessageFailed;
                        var failedEvent = new MessageFailedEvent(
                            failedEvt.EndpointName,
                            failedEvt.MessageTypeName,
                            failedEvt.ExceptionMessage,
                            failedEvt.CorrelationId);

                        lock (_failedListenersLock)
                            foreach (var listener in _failedListeners)
                                listener.Writer.TryWrite(failedEvent);
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
