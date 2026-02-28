using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.Server;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Represents a running test environment: gRPC test host, infrastructure containers
/// (RabbitMQ, PostgreSQL), and endpoint containers. Dispose to stop and tear down
/// everything in the correct order.
/// </summary>
public sealed class TestEnvironment : IAsyncDisposable
{
    readonly TestHostServer _testHost;
    readonly INetwork _network;
    readonly RabbitMqContainer? _rabbitMq;
    readonly PostgreSqlContainer? _postgreSql;
    readonly Dictionary<string, IContainer> _endpointContainers;

    internal TestEnvironment(
        TestHostServer testHost,
        INetwork network,
        RabbitMqContainer? rabbitMq,
        PostgreSqlContainer? postgreSql,
        WireMockServer? wireMock,
        Dictionary<string, IContainer> endpointContainers)
    {
        _testHost = testHost;
        _network = network;
        _rabbitMq = rabbitMq;
        _postgreSql = postgreSql;
        WireMock = wireMock;
        _endpointContainers = endpointContainers;
    }

    /// <summary>
    /// The embedded WireMock stub server, or null if <see cref="TestEnvironmentBuilder.UseWireMock"/>
    /// was not called. Use this to configure stubs before executing scenarios and to verify
    /// calls afterward. Endpoint containers receive WIREMOCK_URL pointing to this server.
    /// </summary>
    public WireMockServer? WireMock { get; }

    /// <summary>Returns a handle to the named endpoint.</summary>
    public EndpointHandle GetEndpoint(string endpointName)
        => _testHost.GetEndpoint(endpointName);

    /// <summary>
    /// Creates an ObserveContext for the given correlation ID. Add conditions with
    /// HandlerInvoked / SagaInvoked / MessageDispatched, then call WhenAllAsync().
    /// </summary>
    public ObserveContext Observe(string correlationId, CancellationToken cancellationToken = default)
        => _testHost.Observe(correlationId, cancellationToken);

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

        await _testHost.DisposeAsync();

        WireMock?.Stop();

        if (_rabbitMq is not null) await _rabbitMq.DisposeAsync();
        if (_postgreSql is not null) await _postgreSql.DisposeAsync();

        await _network.DeleteAsync();
    }
}
