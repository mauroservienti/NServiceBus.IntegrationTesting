using DotNet.Testcontainers.Containers;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// A handle to a named endpoint registered with the test host.
/// Provides endpoint-scoped operations: waiting for the agent to connect,
/// executing scenarios, and resolving host-side mapped ports for any container
/// ports exposed via the <c>containerBuilder</c> callback on
/// <see cref="TestEnvironmentBuilder.AddEndpoint"/>.
/// </summary>
public sealed class EndpointHandle
{
    readonly TestHostGrpcService _grpcService;
    readonly IContainer _container;

    public string EndpointName { get; }

    internal EndpointHandle(TestHostGrpcService grpcService, string endpointName, IContainer container)
    {
        _grpcService = grpcService;
        EndpointName = endpointName;
        _container = container;
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

    /// <summary>
    /// Stops the endpoint container. Resets the agent connection so a subsequent
    /// <see cref="StartAsync"/> waits for the agent to reconnect rather than
    /// returning immediately on a stale completed task.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _grpcService.ResetAgentConnection(EndpointName);
        await _container.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Starts a previously stopped endpoint container and waits for the agent to reconnect.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StartAsync(cancellationToken);
        await _grpcService.WaitForAgentAsync(EndpointName, cancellationToken);
    }

    /// <summary>
    /// Stops and restarts the endpoint container, then waits for the agent to reconnect.
    /// Equivalent to calling <see cref="StopAsync"/> followed by <see cref="StartAsync"/>.
    /// Use this in a <c>[SetUp]</c> method to isolate in-process state between tests
    /// without tearing down the full environment.
    /// </summary>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        _grpcService.ResetAgentConnection(EndpointName);
        await _container.StopAsync(cancellationToken);
        await _container.StartAsync(cancellationToken);
        await _grpcService.WaitForAgentAsync(EndpointName, cancellationToken);
    }

    /// <summary>
    /// Returns the host-side port that Testcontainers mapped to
    /// <paramref name="containerPort"/>. The port must have been declared via
    /// <c>b.WithPortBinding(<paramref name="containerPort"/>, assignRandomHostPort: true)</c>
    /// in the <c>containerBuilder</c> callback passed to
    /// <see cref="TestEnvironmentBuilder.AddEndpoint"/>.
    /// </summary>
    public int GetMappedPort(int containerPort)
        => _container.GetMappedPublicPort(containerPort);

    /// <summary>
    /// Returns a base URL (<c>{scheme}://localhost:{mappedPort}</c>) for the given
    /// container port. The port must have been declared via
    /// <c>b.WithPortBinding(<paramref name="containerPort"/>, assignRandomHostPort: true)</c>
    /// in the <c>containerBuilder</c> callback passed to
    /// <see cref="TestEnvironmentBuilder.AddEndpoint"/>.
    /// </summary>
    public string GetBaseUrl(int containerPort, string scheme = "http")
        => $"{scheme}://localhost:{GetMappedPort(containerPort)}";
}
