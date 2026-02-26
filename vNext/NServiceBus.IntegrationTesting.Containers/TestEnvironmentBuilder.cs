using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace NServiceBus.IntegrationTesting.Containers;

/// <summary>
/// Fluent builder for a complete test environment: Docker network, infrastructure
/// containers (RabbitMQ, PostgreSQL), gRPC test host, and one or more endpoint containers.
/// Call StartAsync() to build and start everything; dispose the returned TestEnvironment
/// when the test fixture tears down.
/// </summary>
public sealed class TestEnvironmentBuilder
{
    string? _rabbitMqImage;
    string? _postgreSqlImage;
    string? _dockerfileDirectory;
    TimeSpan _agentConnectionTimeout = TimeSpan.FromSeconds(120);

    readonly List<(string EndpointName, string Dockerfile)> _endpoints = [];

    /// <summary>
    /// Adds a RabbitMQ container to the environment. All endpoint containers receive a
    /// RABBITMQ_CONNECTION_STRING environment variable pointing to it via the Docker network.
    /// </summary>
    public TestEnvironmentBuilder UseRabbitMq(string image = "rabbitmq:management")
    {
        _rabbitMqImage = image;
        return this;
    }

    /// <summary>
    /// Adds a PostgreSQL container to the environment. All endpoint containers receive a
    /// POSTGRESQL_CONNECTION_STRING environment variable pointing to it via the Docker network.
    /// </summary>
    public TestEnvironmentBuilder UsePostgreSql(string image = "postgres:15.1")
    {
        _postgreSqlImage = image;
        return this;
    }

    /// <summary>
    /// Sets the Docker build context directory used for all AddEndpoint calls.
    /// This is the directory passed as --build-context to Docker (the root from which
    /// COPY and ADD instructions in the Dockerfile are resolved).
    /// </summary>
    public TestEnvironmentBuilder WithDockerfileDirectory(string directory)
    {
        _dockerfileDirectory = directory;
        return this;
    }

    /// <summary>
    /// Registers an endpoint to run as a Docker container.
    /// <paramref name="dockerfile"/> is the path to the Dockerfile relative to the
    /// directory set via WithDockerfileDirectory.
    /// </summary>
    public TestEnvironmentBuilder AddEndpoint(string endpointName, string dockerfile)
    {
        _endpoints.Add((endpointName, dockerfile));
        return this;
    }

    /// <summary>
    /// Overrides the default 120-second timeout for waiting for all agents to connect
    /// after the endpoint containers start.
    /// </summary>
    public TestEnvironmentBuilder WithAgentConnectionTimeout(TimeSpan timeout)
    {
        _agentConnectionTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Starts the full environment: Docker network → infrastructure containers →
    /// gRPC test host → Docker image builds → endpoint containers → agent connection wait.
    /// </summary>
    public async Task<TestEnvironment> StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_dockerfileDirectory))
            throw new InvalidOperationException(
                "Call WithDockerfileDirectory() before StartAsync().");

        // ── Shared Docker network ────────────────────────────────────────────
        var network = new NetworkBuilder().Build();
        await network.CreateAsync(cancellationToken);

        // ── Infrastructure containers ────────────────────────────────────────
        RabbitMqContainer? rabbitMq = null;
        PostgreSqlContainer? postgreSql = null;

        if (_rabbitMqImage is not null)
            rabbitMq = new RabbitMqBuilder(_rabbitMqImage)
                .WithNetwork(network)
                .WithNetworkAliases("rabbitmq")
                .Build();

        if (_postgreSqlImage is not null)
            postgreSql = new PostgreSqlBuilder(_postgreSqlImage)
                .WithNetwork(network)
                .WithNetworkAliases("postgres")
                .Build();

        var infraTasks = new List<Task>();
        if (rabbitMq is not null) infraTasks.Add(rabbitMq.StartAsync(cancellationToken));
        if (postgreSql is not null) infraTasks.Add(postgreSql.StartAsync(cancellationToken));
        await Task.WhenAll(infraTasks);

        // ── gRPC test host ───────────────────────────────────────────────────
        var testHost = new TestHostServer();
        await testHost.StartAsync();

        // ── Shared env vars for all endpoint containers ──────────────────────
        var envVars = new Dictionary<string, string>
        {
            ["NSBUS_TESTING_HOST"] = testHost.ContainerAddress
        };

        if (rabbitMq is not null)
            envVars["RABBITMQ_CONNECTION_STRING"] =
                $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}";

        if (postgreSql is not null)
            envVars["POSTGRESQL_CONNECTION_STRING"] =
                $"Host=postgres;Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
                $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}";

        // ── Build Docker images in parallel ──────────────────────────────────
        var imageEntries = _endpoints
            .Select(ep => (ep.EndpointName, Image: new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(_dockerfileDirectory)
                .WithDockerfile(ep.Dockerfile)
                .Build()))
            .ToList();

        await Task.WhenAll(imageEntries.Select(e => e.Image.CreateAsync(cancellationToken)));

        // ── Start endpoint containers in parallel ────────────────────────────
        var containerEntries = imageEntries
            .Select(e => (e.EndpointName, Container: (IContainer)new ContainerBuilder(e.Image.FullName)
                .WithNetwork(network)
                .WithEnvironment(envVars)
                .Build()))
            .ToList();

        await Task.WhenAll(containerEntries.Select(e => e.Container.StartAsync(cancellationToken)));

        // ── Wait for all agents to connect ───────────────────────────────────
        using var agentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        agentCts.CancelAfter(_agentConnectionTimeout);

        await Task.WhenAll(_endpoints.Select(ep =>
            testHost.GetEndpoint(ep.EndpointName).WaitForConnectedAsync(agentCts.Token)));

        return new TestEnvironment(
            testHost,
            network,
            rabbitMq,
            postgreSql,
            containerEntries.ToDictionary(e => e.EndpointName, e => e.Container));
    }
}
