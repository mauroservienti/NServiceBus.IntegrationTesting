using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using WireMock.Server;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Represents a running test environment: gRPC test host, infrastructure containers,
/// and endpoint containers. Dispose to stop and tear down everything in the correct order.
/// </summary>
public sealed class TestEnvironment : IAsyncDisposable
{
    readonly TestHostServer _testHost;
    readonly INetwork _network;
    readonly Dictionary<string, IContainer> _infraContainers;
    readonly Dictionary<string, IContainer> _endpointContainers;
    readonly Dictionary<string, IContainer> _containers;

    internal TestEnvironment(
        TestHostServer testHost,
        INetwork network,
        WireMockServer? wireMock,
        Dictionary<string, IContainer> infraContainers,
        Dictionary<string, IContainer> endpointContainers,
        Dictionary<string, IContainer> containers)
    {
        _testHost = testHost;
        _network = network;
        WireMock = wireMock;
        _infraContainers = infraContainers;
        _endpointContainers = endpointContainers;
        _containers = containers;
    }

    /// <summary>
    /// The embedded WireMock stub server, or null if <see cref="TestEnvironmentBuilder.UseWireMock"/>
    /// was not called. Use this to configure stubs before executing scenarios and to verify
    /// calls afterward. Endpoint containers receive the WireMock URL as an environment variable.
    /// </summary>
    public WireMockServer? WireMock { get; }

    /// <summary>Returns a handle to the named endpoint.</summary>
    public EndpointHandle GetEndpoint(string endpointName)
    {
        if (!_endpointContainers.TryGetValue(endpointName, out var container))
            throw new InvalidOperationException(
                $"No endpoint named '{endpointName}' was registered.");
        return new EndpointHandle(_testHost.GrpcService, endpointName, container);
    }

    /// <summary>
    /// Returns a handle to the named container registered with
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// </summary>
    public ContainerHandle GetContainer(string name)
    {
        if (!_containers.TryGetValue(name, out var container))
            throw new InvalidOperationException(
                $"No container named '{name}' was registered. " +
                "Use AddContainer() to register agent-less containers.");
        return new ContainerHandle(name, container);
    }

    /// <summary>
    /// Creates an ObserveContext for the given correlation ID. Add conditions with
    /// HandlerInvoked / SagaInvoked / MessageDispatched, then call WhenAllAsync().
    /// </summary>
    public ObserveContext Observe(string correlationId, CancellationToken cancellationToken = default)
        => _testHost.Observe(correlationId, cancellationToken);

    /// <summary>
    /// Stops the named endpoint's container.
    /// Convenience wrapper around <see cref="EndpointHandle.StopAsync"/>.
    /// </summary>
    public Task StopEndpointAsync(string endpointName, CancellationToken cancellationToken = default)
        => GetEndpoint(endpointName).StopAsync(cancellationToken);

    /// <summary>
    /// Starts a previously stopped endpoint container and waits for the agent to reconnect.
    /// Convenience wrapper around <see cref="EndpointHandle.StartAsync"/>.
    /// </summary>
    public Task StartEndpointAsync(string endpointName, CancellationToken cancellationToken = default)
        => GetEndpoint(endpointName).StartAsync(cancellationToken);

    /// <summary>
    /// Restarts the named endpoint's container and waits for the agent to reconnect.
    /// Convenience wrapper around <see cref="EndpointHandle.RestartAsync"/>.
    /// </summary>
    public Task RestartEndpointAsync(string endpointName, CancellationToken cancellationToken = default)
        => GetEndpoint(endpointName).RestartAsync(cancellationToken);

    /// <summary>
    /// Stops the named agent-less container registered with
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// Convenience wrapper around <see cref="ContainerHandle.StopAsync"/>.
    /// </summary>
    public Task StopContainerAsync(string name, CancellationToken cancellationToken = default)
        => GetContainer(name).StopAsync(cancellationToken);

    /// <summary>
    /// Starts a previously stopped agent-less container registered with
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// Convenience wrapper around <see cref="ContainerHandle.StartAsync"/>.
    /// </summary>
    public Task StartContainerAsync(string name, CancellationToken cancellationToken = default)
        => GetContainer(name).StartAsync(cancellationToken);

    /// <summary>
    /// Restarts the named agent-less container registered with
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// Convenience wrapper around <see cref="ContainerHandle.RestartAsync"/>.
    /// </summary>
    public Task RestartContainerAsync(string name, CancellationToken cancellationToken = default)
        => GetContainer(name).RestartAsync(cancellationToken);

    /// <summary>
    /// Returns a handle to the infrastructure container registered under <paramref name="key"/>
    /// (e.g., <see cref="RabbitMqContainerOptions.InfrastructureKey"/> or
    /// <see cref="PostgreSqlContainerOptions.InfrastructureKey"/>).
    /// Use the handle to run commands inside the container, stop/start/restart it, or retrieve its logs.
    /// </summary>
    public InfrastructureHandle GetInfrastructure(string key)
    {
        if (!_infraContainers.TryGetValue(key, out var container))
            throw new InvalidOperationException(
                $"No infrastructure with key '{key}' was registered.");
        return new InfrastructureHandle(key, container);
    }

    /// <summary>
    /// Stops the infrastructure container registered under <paramref name="key"/>.
    /// Convenience wrapper around <see cref="InfrastructureHandle.StopAsync"/>.
    /// </summary>
    public Task StopInfrastructureAsync(string key, CancellationToken cancellationToken = default)
        => GetInfrastructure(key).StopAsync(cancellationToken);

    /// <summary>
    /// Starts a previously stopped infrastructure container registered under <paramref name="key"/>.
    /// Convenience wrapper around <see cref="InfrastructureHandle.StartAsync"/>.
    /// </summary>
    public Task StartInfrastructureAsync(string key, CancellationToken cancellationToken = default)
        => GetInfrastructure(key).StartAsync(cancellationToken);

    /// <summary>
    /// Restarts the infrastructure container registered under <paramref name="key"/>.
    /// Use this to simulate a broker or database restart during chaos / resilience tests.
    /// Convenience wrapper around <see cref="InfrastructureHandle.RestartAsync"/>.
    /// </summary>
    public Task RestartInfrastructureAsync(string key, CancellationToken cancellationToken = default)
        => GetInfrastructure(key).RestartAsync(cancellationToken);

    /// <summary>
    /// Returns the stdout and stderr of the named endpoint's container.
    /// Useful for dumping diagnostic output when a test fails.
    /// </summary>
    public async Task<(string Stdout, string Stderr)> GetEndpointContainerLogsAsync(string endpointName)
    {
        if (!_endpointContainers.TryGetValue(endpointName, out var container))
            throw new InvalidOperationException(
                $"No container registered for endpoint '{endpointName}'.");
        return await container.GetLogsAsync();
    }

    /// <summary>
    /// Stops and disposes all endpoint containers, the gRPC test host, infrastructure
    /// containers, and the Docker network.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(_endpointContainers.Values.Select(c => c.StopAsync()));
        await Task.WhenAll(_endpointContainers.Values.Select(c => c.DisposeAsync().AsTask()));

        await Task.WhenAll(_containers.Values.Select(c => c.StopAsync()));
        await Task.WhenAll(_containers.Values.Select(c => c.DisposeAsync().AsTask()));

        await _testHost.DisposeAsync();

        WireMock?.Stop();

        await Task.WhenAll(_infraContainers.Values.Select(c => c.DisposeAsync().AsTask()));

        await _network.DeleteAsync();
    }
}
