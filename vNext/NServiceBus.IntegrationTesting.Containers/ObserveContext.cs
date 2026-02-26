namespace NServiceBus.IntegrationTesting.Containers;

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

    readonly List<(string Key, Task<HandlerInvokedEvent> Task)> _handlerTasks = [];
    readonly List<(string Key, Task<MessageDispatchedEvent> Task)> _dispatchedTasks = [];

    internal ObserveContext(
        TestHostGrpcService grpcService,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _grpcService = grpcService;
        _correlationId = correlationId;
        _cancellationToken = cancellationToken;
    }

    public ObserveContext HandlerInvoked(string handlerTypeName)
    {
        _handlerTasks.Add((
            handlerTypeName,
            _grpcService.WaitForHandlerInvocationAsync(_correlationId, handlerTypeName, _cancellationToken)));
        return this;
    }

    public ObserveContext MessageDispatched(string messageTypeName)
    {
        _dispatchedTasks.Add((
            messageTypeName,
            _grpcService.WaitForMessageDispatchedAsync(_correlationId, messageTypeName, _cancellationToken)));
        return this;
    }

    /// <summary>
    /// Waits for all registered conditions to be satisfied and returns the results.
    /// </summary>
    public async Task<ObserveResults> WhenAllAsync()
    {
        IEnumerable<Task> allTasks = _handlerTasks.Select(t => (Task)t.Task)
            .Concat(_dispatchedTasks.Select(t => (Task)t.Task));

        await Task.WhenAll(allTasks);

        return new ObserveResults(
            _handlerTasks.ToDictionary(t => t.Key, t => t.Task.Result),
            _dispatchedTasks.ToDictionary(t => t.Key, t => t.Task.Result));
    }
}

/// <summary>
/// Holds the results of all observed conditions after WhenAllAsync completes.
/// Access results using the same type names used when registering conditions.
/// </summary>
public sealed class ObserveResults
{
    readonly Dictionary<string, HandlerInvokedEvent> _handlerResults;
    readonly Dictionary<string, MessageDispatchedEvent> _dispatchedResults;

    internal ObserveResults(
        Dictionary<string, HandlerInvokedEvent> handlerResults,
        Dictionary<string, MessageDispatchedEvent> dispatchedResults)
    {
        _handlerResults = handlerResults;
        _dispatchedResults = dispatchedResults;
    }

    public HandlerInvokedEvent HandlerInvoked(string handlerTypeName)
        => _handlerResults.TryGetValue(handlerTypeName, out var evt)
            ? evt
            : throw new InvalidOperationException(
                $"No HandlerInvokedEvent for '{handlerTypeName}' was observed.");

    public MessageDispatchedEvent MessageDispatched(string messageTypeName)
        => _dispatchedResults.TryGetValue(messageTypeName, out var evt)
            ? evt
            : throw new InvalidOperationException(
                $"No MessageDispatchedEvent for '{messageTypeName}' was observed.");
}
