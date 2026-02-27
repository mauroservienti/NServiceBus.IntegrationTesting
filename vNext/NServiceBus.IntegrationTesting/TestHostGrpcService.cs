using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using NServiceBus.IntegrationTesting.Proto;

namespace NServiceBus.IntegrationTesting;

public sealed record HandlerInvokedEvent(
    string EndpointName,
    string HandlerTypeName,
    string MessageTypeName,
    string CorrelationId,
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
    string CorrelationId);

public sealed record MessageFailedEvent(
    string EndpointName,
    string MessageTypeName,
    string ExceptionMessage,
    string CorrelationId);

/// <summary>
/// Thrown by WaitFor* methods when the message with the watched correlation ID is
/// permanently sent to the error queue before the expected event arrives.
/// Allows tests to fail fast with a descriptive message instead of waiting
/// for the test timeout to expire.
/// </summary>
public sealed class MessageFailedException(string correlationId, string messageTypeName, string causeMessage)
    : Exception($"Message '{messageTypeName}' was sent to the error queue for correlation '{correlationId}'. Cause: {causeMessage}")
{
    public string CorrelationId { get; } = correlationId;
    public string MessageTypeName { get; } = messageTypeName;
}

/// <summary>
/// gRPC service implementation that runs in the test host process.
/// Agents (in endpoint processes/containers) connect to this service.
/// </summary>
internal sealed class TestHostGrpcService : TestHostService.TestHostServiceBase
{
    // One TaskCompletionSource per expected endpoint name.
    // Completes when the agent for that endpoint connects.
    readonly ConcurrentDictionary<string, TaskCompletionSource> _agentConnections = new();

    // Per-agent outbound channels. The background ForwardCommandsAsync task
    // drains each channel and writes to the agent's response stream.
    readonly ConcurrentDictionary<string, Channel<HostToAgentMessage>> _commandChannels = new();

    // Fan-out listener lists — each WaitFor call registers its own channel and
    // receives a copy of every event so concurrent tests can filter independently.
    readonly object _handlerListenersLock = new();
    readonly List<Channel<HandlerInvokedEvent>> _handlerListeners = [];

    readonly object _dispatchedListenersLock = new();
    readonly List<Channel<MessageDispatchedEvent>> _dispatchedListeners = [];

    readonly object _failedListenersLock = new();
    readonly List<Channel<MessageFailedEvent>> _failedListeners = [];

    /// <summary>
    /// Creates an ObserveContext for the given correlation ID. Add conditions with
    /// HandlerInvoked / MessageDispatched, then call WhenAllAsync() to wait for all of them.
    /// All listeners are registered immediately — no events can be missed between calls.
    /// </summary>
    public ObserveContext Observe(string correlationId, CancellationToken cancellationToken = default)
        => new(this, correlationId, cancellationToken);

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
    /// Convenience overload: returns the first HandlerInvokedEvent for the given
    /// correlation ID and handler type name.
    /// </summary>
    public async Task<HandlerInvokedEvent> WaitForHandlerInvocationAsync(
        string correlationId,
        string handlerTypeName,
        CancellationToken cancellationToken = default)
    {
        var results = await WaitForHandlerInvocationsAsync(
            correlationId, handlerTypeName,
            until: static all => all.Count >= 1,
            cancellationToken);
        return results[0];
    }

    /// <summary>
    /// Returns a Task that completes when the accumulated HandlerInvokedEvents for the
    /// given correlation ID and handler type name satisfy <paramref name="until"/>.
    /// The predicate receives the growing list of matching events (oldest first) and
    /// should return true when enough events have been collected.
    /// Only successful invocations are counted — transient failures are not surfaced here.
    /// Races against MessageFailedEvent: if the message is permanently dead before the
    /// predicate is satisfied, throws MessageFailedException immediately.
    /// Each call registers an independent listener — safe for concurrent tests.
    /// </summary>
    public async Task<IReadOnlyList<HandlerInvokedEvent>> WaitForHandlerInvocationsAsync(
        string correlationId,
        string handlerTypeName,
        Func<IReadOnlyList<HandlerInvokedEvent>, bool> until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(until);

        var handlerChannel = Channel.CreateUnbounded<HandlerInvokedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
        var failureChannel = Channel.CreateUnbounded<MessageFailedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        lock (_handlerListenersLock)
            _handlerListeners.Add(handlerChannel);
        lock (_failedListenersLock)
            _failedListeners.Add(failureChannel);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var successTask = ScanForHandlerEventsAsync(
                handlerChannel.Reader, correlationId, handlerTypeName, until, cts.Token);
            var failureTask = ScanForFailureEventAsync(
                failureChannel.Reader, correlationId, cts.Token);

            await Task.WhenAny(successTask, failureTask);
            await cts.CancelAsync();

            var handlerResults = await successTask;
            var failureResult = await failureTask;

            if (failureResult is not null)
                throw new MessageFailedException(
                    correlationId, failureResult.MessageTypeName, failureResult.ExceptionMessage);

            if (handlerResults is not null)
                return handlerResults;

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(
                $"Timed out waiting for '{handlerTypeName}' invocation(s) for correlation '{correlationId}'.",
                cancellationToken);
        }
        finally
        {
            lock (_handlerListenersLock)
                _handlerListeners.Remove(handlerChannel);
            handlerChannel.Writer.TryComplete();

            lock (_failedListenersLock)
                _failedListeners.Remove(failureChannel);
            failureChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Convenience overload: returns the first MessageDispatchedEvent for the given
    /// correlation ID and message type name.
    /// </summary>
    public async Task<MessageDispatchedEvent> WaitForMessageDispatchedAsync(
        string correlationId,
        string messageTypeName,
        CancellationToken cancellationToken = default)
    {
        var results = await WaitForMessageDispatchesAsync(
            correlationId, messageTypeName,
            until: static all => all.Count >= 1,
            cancellationToken);
        return results[0];
    }

    /// <summary>
    /// Returns a Task that completes when the accumulated MessageDispatchedEvents for the
    /// given correlation ID and message type name satisfy <paramref name="until"/>.
    /// The predicate receives the growing list of matching events (oldest first) and
    /// should return true when enough events have been collected.
    /// Only successful dispatches are counted — transient failures are not surfaced here.
    /// Races against MessageFailedEvent: if the message is permanently dead before the
    /// predicate is satisfied, throws MessageFailedException immediately.
    /// Each call registers an independent listener — safe for concurrent tests.
    /// </summary>
    public async Task<IReadOnlyList<MessageDispatchedEvent>> WaitForMessageDispatchesAsync(
        string correlationId,
        string messageTypeName,
        Func<IReadOnlyList<MessageDispatchedEvent>, bool> until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(until);

        var dispatchChannel = Channel.CreateUnbounded<MessageDispatchedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
        var failureChannel = Channel.CreateUnbounded<MessageFailedEvent>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        lock (_dispatchedListenersLock)
            _dispatchedListeners.Add(dispatchChannel);
        lock (_failedListenersLock)
            _failedListeners.Add(failureChannel);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var successTask = ScanForDispatchEventsAsync(
                dispatchChannel.Reader, correlationId, messageTypeName, until, cts.Token);
            var failureTask = ScanForFailureEventAsync(
                failureChannel.Reader, correlationId, cts.Token);

            await Task.WhenAny(successTask, failureTask);
            await cts.CancelAsync();

            var dispatchResults = await successTask;
            var failureResult = await failureTask;

            if (failureResult is not null)
                throw new MessageFailedException(
                    correlationId, failureResult.MessageTypeName, failureResult.ExceptionMessage);

            if (dispatchResults is not null)
                return dispatchResults;

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(
                $"Timed out waiting for '{messageTypeName}' dispatch(es) for correlation '{correlationId}'.",
                cancellationToken);
        }
        finally
        {
            lock (_dispatchedListenersLock)
                _dispatchedListeners.Remove(dispatchChannel);
            dispatchChannel.Writer.TryComplete();

            lock (_failedListenersLock)
                _failedListeners.Remove(failureChannel);
            failureChannel.Writer.TryComplete();
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
    /// Returns the correlation ID that ties all events produced by this execution together.
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
                            dispatchedEvt.CorrelationId);

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

    // Scan helpers return null when the token is cancelled or the channel is closed
    // without collecting enough matching events — the caller interprets null as "not found".

    internal static async Task<IReadOnlyList<HandlerInvokedEvent>?> ScanForHandlerEventsAsync(
        ChannelReader<HandlerInvokedEvent> reader,
        string correlationId,
        string handlerTypeName,
        Func<IReadOnlyList<HandlerInvokedEvent>, bool> isDone,
        CancellationToken cancellationToken)
    {
        var collected = new List<HandlerInvokedEvent>();
        try
        {
            await foreach (var evt in reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId && evt.HandlerTypeName == handlerTypeName)
                {
                    collected.Add(evt);
                    if (isDone(collected))
                        return collected;
                }
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    internal static async Task<IReadOnlyList<MessageDispatchedEvent>?> ScanForDispatchEventsAsync(
        ChannelReader<MessageDispatchedEvent> reader,
        string correlationId,
        string messageTypeName,
        Func<IReadOnlyList<MessageDispatchedEvent>, bool> isDone,
        CancellationToken cancellationToken)
    {
        var collected = new List<MessageDispatchedEvent>();
        try
        {
            await foreach (var evt in reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId && evt.MessageTypeName == messageTypeName)
                {
                    collected.Add(evt);
                    if (isDone(collected))
                        return collected;
                }
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    internal static async Task<MessageFailedEvent?> ScanForFailureEventAsync(
        ChannelReader<MessageFailedEvent> reader,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var evt in reader.ReadAllAsync(cancellationToken))
            {
                if (evt.CorrelationId == correlationId)
                    return evt;
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }
}
