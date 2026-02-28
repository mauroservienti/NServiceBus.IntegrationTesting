using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.Server;
using WireMock.Settings;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Fluent builder for a complete test environment: Docker network, infrastructure
/// containers (RabbitMQ, PostgreSQL), gRPC test host, and one or more endpoint containers.
/// Call StartAsync() to build and start everything; dispose the returned TestEnvironment
/// when the test fixture tears down.
/// </summary>
public sealed class TestEnvironmentBuilder
{
    RabbitMqContainerOptions? _rabbitMqOptions;
    PostgreSqlContainerOptions? _postgreSqlOptions;
    string? _dockerfileDirectory;
    bool _useWireMock;
    TimeSpan _agentConnectionTimeout = TimeSpan.FromSeconds(120);

    readonly List<EndpointRegistration> _endpoints = [];

    record EndpointRegistration(string EndpointName, string Dockerfile, EndpointContainerOptions Options);

    /// <summary>
    /// Adds a RabbitMQ container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the environment variable name (default: <c>RABBITMQ_CONNECTION_STRING</c>).
    /// </summary>
    public TestEnvironmentBuilder UseRabbitMQ(Action<RabbitMqContainerOptions>? configure = null)
    {
        _rabbitMqOptions = new RabbitMqContainerOptions();
        configure?.Invoke(_rabbitMqOptions);
        return this;
    }

    /// <summary>
    /// Adds a PostgreSQL container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the environment variable name (default: <c>POSTGRESQL_CONNECTION_STRING</c>).
    /// </summary>
    public TestEnvironmentBuilder UsePostgreSql(Action<PostgreSqlContainerOptions>? configure = null)
    {
        _postgreSqlOptions = new PostgreSqlContainerOptions();
        configure?.Invoke(_postgreSqlOptions);
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
    /// Use the optional <paramref name="configure"/> callback to override per-endpoint
    /// environment variable names (e.g. for RabbitMQ, PostgreSQL, WireMock) or to
    /// inject additional static environment variables.
    /// </summary>
    public TestEnvironmentBuilder AddEndpoint(string endpointName, string dockerfile,
        Action<EndpointContainerOptions>? configure = null)
    {
        if (_endpoints.Any(e => e.EndpointName == endpointName))
            throw new ArgumentException(
                $"An endpoint named '{endpointName}' has already been added.", nameof(endpointName));

        var options = new EndpointContainerOptions();
        configure?.Invoke(options);
        _endpoints.Add(new EndpointRegistration(endpointName, dockerfile, options));
        return this;
    }

    /// <summary>
    /// Starts an embedded WireMock.Net server in the test process. All endpoint containers
    /// receive a WIREMOCK_URL environment variable pointing to it via host.docker.internal.
    /// Access the server via <see cref="TestEnvironment.WireMock"/> to configure stubs
    /// and verify calls.
    /// </summary>
    public TestEnvironmentBuilder UseWireMock()
    {
        _useWireMock = true;
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
    /// If any step fails, all resources that were successfully started are disposed before
    /// the exception propagates.
    /// </summary>
    public async Task<TestEnvironment> StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_dockerfileDirectory))
            throw new InvalidOperationException(
                "Call WithDockerfileDirectory() before StartAsync().");

        // Declare all resources upfront so the catch block can clean up whatever was
        // successfully created even if startup fails partway through.
        INetwork? network = null;
        RabbitMqContainer? rabbitMq = null;
        PostgreSqlContainer? postgreSql = null;
        TestHostServer? testHost = null;
        WireMockServer? wireMock = null;
        List<(string EndpointName, IContainer Container)> containerEntries = [];

        try
        {
            // ── Shared Docker network ────────────────────────────────────────────
            network = new NetworkBuilder().Build();
            await network.CreateAsync(cancellationToken);

            // ── Infrastructure containers ────────────────────────────────────────
            if (_rabbitMqOptions is not null)
                rabbitMq = new RabbitMqBuilder(_rabbitMqOptions.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases("rabbitmq")
                    .Build();

            if (_postgreSqlOptions is not null)
                postgreSql = new PostgreSqlBuilder(_postgreSqlOptions.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases("postgres")
                    .Build();

            var infraTasks = new List<Task>();
            if (rabbitMq is not null) infraTasks.Add(rabbitMq.StartAsync(cancellationToken));
            if (postgreSql is not null) infraTasks.Add(postgreSql.StartAsync(cancellationToken));
            await Task.WhenAll(infraTasks);

            // ── gRPC test host ───────────────────────────────────────────────────
            testHost = new TestHostServer();
            await testHost.StartAsync();

            // ── WireMock stub server (embedded, in test process) ─────────────────
            // Bind to 0.0.0.0 so Docker containers can reach it via host.docker.internal.
            // Pre-allocate a free port to avoid a two-step start, then release it and hand
            // the port to WireMock — the race window is negligible in test environments.
            if (_useWireMock)
            {
                int wireMockPort;
                using (var probe = new TcpListener(IPAddress.Any, 0))
                {
                    probe.Start();
                    wireMockPort = ((IPEndPoint)probe.LocalEndpoint).Port;
                }
                wireMock = WireMockServer.Start(new WireMockServerSettings
                {
                    UseSSL = false,
                    Urls = [$"http://0.0.0.0:{wireMockPort}"]
                });
            }

            // ── Precompute infrastructure connection string values ────────────────
            // Values are fixed regardless of per-endpoint env var name choices.
            var testHostAddress = testHost.ContainerAddress;

            var rabbitMqConnectionString = rabbitMq is not null
                ? $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}"
                : null;

            var postgreSqlConnectionString = postgreSql is not null
                ? $"Host=postgres;Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
                  $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}"
                : null;

            var wireMockUrl = wireMock is not null
                ? $"http://host.docker.internal:{wireMock.Port}"
                : null;

            // ── Build Docker images in parallel ──────────────────────────────────
            // Use a fixed, predictable image name so CI can pre-build with the exact same tag.
            // When the pre-built image already exists, Docker finds a full cache hit on every
            // layer AND the tag itself — BuildImageFromDockerfileAsync returns synchronously
            // and the subsequent ExistsWithIdAsync check succeeds, avoiding the BuildKit race
            // condition where the async tagging completes after Testcontainers checks.
            var imageEntries = _endpoints
                .Select(ep => (ep.EndpointName, ep.Options, Image: new ImageFromDockerfileBuilder()
                    .WithDockerfileDirectory(_dockerfileDirectory)
                    .WithDockerfile(ep.Dockerfile)
                    .WithName($"localhost/nsb-integration-testing/{ep.EndpointName.ToLowerInvariant()}:latest")
                    .Build()))
                .ToList();

            await Task.WhenAll(imageEntries.Select(e => e.Image.CreateAsync(cancellationToken)));

            // ── Start endpoint containers in parallel ────────────────────────────
            // Env vars are built per-endpoint so each can use its own naming conventions.
            containerEntries = imageEntries
                .Select(e =>
                {
                    var envVars = new Dictionary<string, string>
                    {
                        ["NSBUS_TESTING_HOST"] = testHostAddress
                    };

                    if (rabbitMqConnectionString is not null)
                        envVars[e.Options.RabbitMqConnectionStringEnvVarName
                            ?? _rabbitMqOptions!.ConnectionStringEnvVarName] = rabbitMqConnectionString;

                    if (postgreSqlConnectionString is not null)
                        envVars[e.Options.PostgreSqlConnectionStringEnvVarName
                            ?? _postgreSqlOptions!.ConnectionStringEnvVarName] = postgreSqlConnectionString;

                    if (wireMockUrl is not null)
                        envVars[e.Options.WireMockUrlEnvVarName ?? "WIREMOCK_URL"] = wireMockUrl;

                    foreach (var (key, value) in e.Options.EnvironmentVariables)
                        envVars[key] = value;

                    return (e.EndpointName, Container: (IContainer)new ContainerBuilder(e.Image.FullName)
                        .WithNetwork(network)
                        .WithEnvironment(envVars)
                        .WithExtraHost("host.docker.internal", "host-gateway")
                        .Build());
                })
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
                wireMock,
                containerEntries.ToDictionary(e => e.EndpointName, e => e.Container));
        }
        catch
        {
            // Best-effort cleanup: attempt each disposal independently so a failure in
            // one step does not prevent the remaining resources from being released.
            foreach (var (_, container) in containerEntries)
            {
                try { await container.StopAsync(); } catch { }
                try { await container.DisposeAsync(); } catch { }
            }

            if (testHost is not null)
                try { await testHost.DisposeAsync(); } catch { }

            wireMock?.Stop();

            if (rabbitMq is not null)
                try { await rabbitMq.DisposeAsync(); } catch { }

            if (postgreSql is not null)
                try { await postgreSql.DisposeAsync(); } catch { }

            if (network is not null)
                try { await network.DeleteAsync(); } catch { }

            throw;
        }
    }
}
