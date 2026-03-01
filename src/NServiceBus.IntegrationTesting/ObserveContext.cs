namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Collects a set of done conditions for a given correlation ID and waits for all of them.
/// All listeners are registered immediately when each condition is added — no events can be
/// missed between condition registrations, eliminating the pre-registration footgun.
/// If any message with the observed correlation ID is permanently sent to the error queue,
/// WhenAllAsync throws MessageFailedException immediately.
/// </summary>
public sealed class ObserveContext
{
    readonly TestHostGrpcService _grpcService;
    readonly string _correlationId;
    readonly CancellationToken _cancellationToken;

    readonly List<(string TypeName, Task<IReadOnlyList<HandlerInvokedEvent>> Task)> _handlerTasks = [];
    readonly List<(string TypeName, Task<IReadOnlyList<HandlerInvokedEvent>> Task)> _sagaTasks = [];
    readonly List<(string TypeName, Task<IReadOnlyList<MessageDispatchedEvent>> Task)> _dispatchedTasks = [];
    Task<MessageFailedEvent>? _failedTask;

    internal ObserveContext(
        TestHostGrpcService grpcService,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _grpcService = grpcService;
        _correlationId = correlationId;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Wait for the first successful invocation of the named message handler.
    /// </summary>
    public ObserveContext HandlerInvoked(string handlerTypeName)
        => HandlerInvoked(handlerTypeName, static all => all.Count >= 1);

    /// <summary>
    /// Wait for invocations of the named message handler until <paramref name="until"/> returns true.
    /// The predicate receives the latest event and returns true when done.
    /// </summary>
    public ObserveContext HandlerInvoked(string handlerTypeName, Func<HandlerInvokedEvent, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        return HandlerInvoked(handlerTypeName, all => until(all[^1]));
    }

    /// <summary>
    /// Wait for invocations of the named message handler until <paramref name="until"/> returns true.
    /// The predicate receives the growing list of matching events (oldest first).
    /// </summary>
    public ObserveContext HandlerInvoked(string handlerTypeName, Func<IReadOnlyList<HandlerInvokedEvent>, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        if (_failedTask is not null)
            throw new InvalidOperationException(
                "Cannot register HandlerInvoked() when MessageFailed() is already registered. " +
                "Use MessageFailed() alone when the test expects a failure outcome.");
        _handlerTasks.Add((
            handlerTypeName,
            _grpcService.WaitForHandlerInvocationsAsync(_correlationId, handlerTypeName, until, _cancellationToken)));
        return this;
    }

    /// <summary>
    /// Wait for the first successful invocation of the named saga.
    /// </summary>
    public ObserveContext SagaInvoked(string sagaTypeName)
        => SagaInvoked(sagaTypeName, static all => all.Count >= 1);

    /// <summary>
    /// Wait for invocations of the named saga until <paramref name="until"/> returns true.
    /// The predicate receives the latest event and returns true when done.
    /// </summary>
    public ObserveContext SagaInvoked(string sagaTypeName, Func<HandlerInvokedEvent, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        return SagaInvoked(sagaTypeName, all => until(all[^1]));
    }

    /// <summary>
    /// Wait for invocations of the named saga until <paramref name="until"/> returns true.
    /// The predicate receives the growing list of matching events (oldest first).
    /// </summary>
    public ObserveContext SagaInvoked(string sagaTypeName, Func<IReadOnlyList<HandlerInvokedEvent>, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        if (_failedTask is not null)
            throw new InvalidOperationException(
                "Cannot register SagaInvoked() when MessageFailed() is already registered. " +
                "Use MessageFailed() alone when the test expects a failure outcome.");
        _sagaTasks.Add((
            sagaTypeName,
            _grpcService.WaitForSagaInvocationsAsync(_correlationId, sagaTypeName, until, _cancellationToken)));
        return this;
    }

    /// <summary>
    /// Wait for the first successful dispatch of the named message type.
    /// </summary>
    public ObserveContext MessageDispatched(string messageTypeName)
        => MessageDispatched(messageTypeName, static all => all.Count >= 1);

    /// <summary>
    /// Wait for dispatches of the named message type until <paramref name="until"/> returns true.
    /// The predicate receives the latest event and returns true when done.
    /// </summary>
    public ObserveContext MessageDispatched(string messageTypeName, Func<MessageDispatchedEvent, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        return MessageDispatched(messageTypeName, all => until(all[^1]));
    }

    /// <summary>
    /// Wait for dispatches of the named message type until <paramref name="until"/> returns true.
    /// The predicate receives the growing list of matching events (oldest first).
    /// </summary>
    public ObserveContext MessageDispatched(string messageTypeName, Func<IReadOnlyList<MessageDispatchedEvent>, bool> until)
    {
        ArgumentNullException.ThrowIfNull(until);
        if (_failedTask is not null)
            throw new InvalidOperationException(
                "Cannot register MessageDispatched() when MessageFailed() is already registered. " +
                "Use MessageFailed() alone when the test expects a failure outcome.");
        _dispatchedTasks.Add((
            messageTypeName,
            _grpcService.WaitForMessageDispatchesAsync(_correlationId, messageTypeName, until, _cancellationToken)));
        return this;
    }

    /// <summary>
    /// Wait for a message with this correlation ID to be permanently sent to the error queue.
    /// Use this as the sole condition when the test expects a failure outcome.
    /// </summary>
    public ObserveContext MessageFailed()
    {
        if (_failedTask is not null)
            throw new InvalidOperationException(
                "MessageFailed() has already been registered for this ObserveContext.");

        if (_handlerTasks.Count > 0 || _sagaTasks.Count > 0 || _dispatchedTasks.Count > 0)
            throw new InvalidOperationException(
                "Cannot register MessageFailed() when success conditions are already registered. " +
                "Use MessageFailed() alone when the test expects a failure outcome.");

        _failedTask = _grpcService.WaitForMessageFailureAsync(_correlationId, _cancellationToken);
        return this;
    }

    /// <summary>
    /// Waits for all registered conditions to be satisfied and returns the results.
    /// </summary>
    public async Task<ObserveResults> WhenAllAsync()
    {
        if (_handlerTasks.Count == 0 && _sagaTasks.Count == 0 && _dispatchedTasks.Count == 0 && _failedTask is null)
            throw new InvalidOperationException(
                "No conditions were registered. Call at least one of HandlerInvoked, SagaInvoked, " +
                "MessageDispatched, or MessageFailed before awaiting WhenAllAsync.");

        IEnumerable<Task> allTasks = _handlerTasks.Select(t => (Task)t.Task)
            .Concat(_sagaTasks.Select(t => (Task)t.Task))
            .Concat(_dispatchedTasks.Select(t => (Task)t.Task));

        if (_failedTask is not null)
            allTasks = allTasks.Append(_failedTask);

        await Task.WhenAll(allTasks);

        return new ObserveResults(
            _handlerTasks.ToDictionary(t => t.TypeName, t => t.Task.Result),
            _sagaTasks.ToDictionary(t => t.TypeName, t => t.Task.Result),
            _dispatchedTasks.ToDictionary(t => t.TypeName, t => t.Task.Result),
            _failedTask?.Result);
    }
}

/// <summary>
/// Holds the results of all observed conditions after WhenAllAsync completes.
/// </summary>
public sealed class ObserveResults
{
    readonly Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> _handlerResults;
    readonly Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> _sagaResults;
    readonly Dictionary<string, IReadOnlyList<MessageDispatchedEvent>> _dispatchedResults;
    readonly MessageFailedEvent? _failedResult;

    internal ObserveResults(
        Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> handlerResults,
        Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> sagaResults,
        Dictionary<string, IReadOnlyList<MessageDispatchedEvent>> dispatchedResults,
        MessageFailedEvent? failedResult = null)
    {
        _handlerResults = handlerResults;
        _sagaResults = sagaResults;
        _dispatchedResults = dispatchedResults;
        _failedResult = failedResult;
    }

    /// <summary>
    /// All collected invocations of the named message handler satisfying the condition
    /// registered via ObserveContext.HandlerInvoked.
    /// </summary>
    public IReadOnlyList<HandlerInvokedEvent> HandlerInvocations(string handlerTypeName)
        => _handlerResults.TryGetValue(handlerTypeName, out var list)
            ? list
            : throw new InvalidOperationException(
                $"No HandlerInvokedEvents for '{handlerTypeName}' were observed.");

    /// <summary>
    /// The last (or only) invocation of the named message handler.
    /// Equivalent to HandlerInvocations(name).Last() — convenient for the no-arg overload.
    /// </summary>
    public HandlerInvokedEvent HandlerInvoked(string handlerTypeName)
        => HandlerInvocations(handlerTypeName).Last();

    /// <summary>
    /// All collected invocations of the named saga satisfying the condition
    /// registered via ObserveContext.SagaInvoked.
    /// </summary>
    public IReadOnlyList<HandlerInvokedEvent> SagaInvocations(string sagaTypeName)
        => _sagaResults.TryGetValue(sagaTypeName, out var list)
            ? list
            : throw new InvalidOperationException(
                $"No SagaInvokedEvents for '{sagaTypeName}' were observed.");

    /// <summary>
    /// The last (or only) invocation of the named saga.
    /// Equivalent to SagaInvocations(name).Last() — convenient for the no-arg overload.
    /// </summary>
    public HandlerInvokedEvent SagaInvoked(string sagaTypeName)
        => SagaInvocations(sagaTypeName).Last();

    /// <summary>
    /// All collected dispatches of the named message type satisfying the condition
    /// registered via ObserveContext.MessageDispatched.
    /// </summary>
    public IReadOnlyList<MessageDispatchedEvent> MessageDispatches(string messageTypeName)
        => _dispatchedResults.TryGetValue(messageTypeName, out var list)
            ? list
            : throw new InvalidOperationException(
                $"No MessageDispatchedEvents for '{messageTypeName}' were observed.");

    /// <summary>
    /// The last (or only) dispatch of the named message type.
    /// Equivalent to MessageDispatches(name).Last() — convenient for the no-arg overload.
    /// </summary>
    public MessageDispatchedEvent MessageDispatched(string messageTypeName)
        => MessageDispatches(messageTypeName).Last();

    /// <summary>
    /// The failure event recorded when a message was permanently sent to the error queue.
    /// Only available when ObserveContext.MessageFailed() was registered.
    /// </summary>
    public MessageFailedEvent MessageFailed()
        => _failedResult
            ?? throw new InvalidOperationException(
                "No MessageFailed condition was registered via ObserveContext.MessageFailed().");
}
