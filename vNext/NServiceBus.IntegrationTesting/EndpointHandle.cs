namespace NServiceBus.IntegrationTesting;

/// <summary>
/// A handle to a named endpoint registered with the test host.
/// Provides endpoint-scoped operations: waiting for the agent to connect
/// and executing scenarios.
/// </summary>
public sealed class EndpointHandle
{
    readonly TestHostGrpcService _grpcService;

    public string EndpointName { get; }

    internal EndpointHandle(TestHostGrpcService grpcService, string endpointName)
    {
        _grpcService = grpcService;
        EndpointName = endpointName;
    }

    /// <summary>
    /// Returns a Task that completes when this endpoint's agent connects to the test host.
    /// </summary>
    internal Task WaitForConnectedAsync(CancellationToken cancellationToken = default)
        => _grpcService.WaitForAgentAsync(EndpointName, cancellationToken);

    /// <summary>
    /// Instructs the endpoint's agent to execute a registered scenario by name.
    /// Returns the correlation ID that ties all events produced by this execution together.
    /// </summary>
    public Task<string> ExecuteScenarioAsync(
        string scenarioName,
        Dictionary<string, string>? args = null,
        CancellationToken cancellationToken = default)
        => _grpcService.ExecuteScenarioAsync(EndpointName, scenarioName, args, cancellationToken);
}
