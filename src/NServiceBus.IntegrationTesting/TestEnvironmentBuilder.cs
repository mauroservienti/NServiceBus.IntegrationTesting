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
/// containers, gRPC test host, and one or more endpoint containers.
/// Call StartAsync() to build and start everything; dispose the returned TestEnvironment
/// when the test fixture tears down.
/// </summary>
public sealed class TestEnvironmentBuilder
{
    string? _dockerfileDirectory;
    WireMockOptions? _wireMockOptions;
    TimeSpan _agentConnectionTimeout = TimeSpan.FromSeconds(120);

    readonly List<InfrastructureDeclaration> _infrastructure = [];
    readonly List<EndpointRegistration> _endpoints = [];

    record InfrastructureDeclaration(
        string Key,
        string DefaultEnvVarName,
        Func<INetwork, IContainer> BuildContainer,
        string ConnectionString);

    record EndpointRegistration(string EndpointName, string Dockerfile, EndpointContainerOptions Options);

    /// <summary>
    /// Adds a RabbitMQ container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>RABBITMQ_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="RabbitMqContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public TestEnvironmentBuilder UseRabbitMQ(Action<RabbitMqContainerOptions>? configure = null)
    {
        var opts = new RabbitMqContainerOptions();
        configure?.Invoke(opts);
        return UseInfrastructure(
            RabbitMqContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new RabbitMqBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("rabbitmq")
                .Build(),
            $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}");
    }

    /// <summary>
    /// Adds a PostgreSQL container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>POSTGRESQL_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="PostgreSqlContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public TestEnvironmentBuilder UsePostgreSql(Action<PostgreSqlContainerOptions>? configure = null)
    {
        var opts = new PostgreSqlContainerOptions();
        configure?.Invoke(opts);
        return UseInfrastructure(
            PostgreSqlContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new PostgreSqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("postgres")
                .Build(),
            $"Host=postgres;Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
            $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}");
    }

    /// <summary>
    /// Registers a custom infrastructure container. The container is started on the shared
    /// Docker network before any endpoint containers. All endpoint containers receive the
    /// <paramref name="connectionString"/> value injected under
    /// <paramref name="defaultEnvVarName"/> (or a per-endpoint override set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <paramref name="key"/>). Use <paramref name="additionalEnvironmentVariables"/> to
    /// inject any extra env vars from this infrastructure into all endpoint containers.
    /// </summary>
    public TestEnvironmentBuilder UseInfrastructure(
        string key,
        string defaultEnvVarName,
        Func<INetwork, IContainer> buildContainer,
        string connectionString)
    {
        if (_infrastructure.Any(i => i.Key == key))
            throw new ArgumentException(
                $"Infrastructure with key '{key}' has already been registered.", nameof(key));

        _infrastructure.Add(new InfrastructureDeclaration(key, defaultEnvVarName, buildContainer, connectionString));
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
    /// environment variable names via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> or to inject
    /// additional static environment variables.
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
    /// receive the WireMock URL as an environment variable (default name: <c>WIREMOCK_URL</c>).
    /// Use the optional <paramref name="configure"/> callback to override the variable name.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="WireMockOptions.InfrastructureKey"/>.
    /// Access the server via <see cref="TestEnvironment.WireMock"/>.
    /// </summary>
    public TestEnvironmentBuilder UseWireMock(Action<WireMockOptions>? configure = null)
    {
        _wireMockOptions = new WireMockOptions();
        configure?.Invoke(_wireMockOptions);
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
        List<IContainer> infraContainers = [];
        TestHostServer? testHost = null;
        WireMockServer? wireMock = null;
        List<(string EndpointName, IContainer Container)> containerEntries = [];

        try
        {
            // ── Shared Docker network ────────────────────────────────────────────
            network = new NetworkBuilder().Build();
            await network.CreateAsync(cancellationToken);

            // ── Infrastructure containers ────────────────────────────────────────
            infraContainers.AddRange(_infrastructure.Select(decl => decl.BuildContainer(network)));
            await Task.WhenAll(infraContainers.Select(c => c.StartAsync(cancellationToken)));

            // ── gRPC test host ───────────────────────────────────────────────────
            testHost = new TestHostServer();
            await testHost.StartAsync();

            // ── WireMock stub server (embedded, in test process) ─────────────────
            // Bind to 0.0.0.0 so Docker containers can reach it via host.docker.internal.
            // Pre-allocate a free port to avoid a two-step start, then release it and hand
            // the port to WireMock — the race window is negligible in test environments.
            if (_wireMockOptions is not null)
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
            // Env vars are built per-endpoint: infrastructure connection strings are injected
            // under the name specified in InfrastructureEnvVarNames (endpoint override) or the
            // DefaultEnvVarName on the declaration (global default).
            var testHostAddress = testHost.ContainerAddress;
            var wireMockUrl = wireMock is not null
                ? $"http://host.docker.internal:{wireMock.Port}"
                : null;

            containerEntries = imageEntries
                .Select(e =>
                {
                    var envVars = new Dictionary<string, string>
                    {
                        ["NSBUS_TESTING_HOST"] = testHostAddress
                    };

                    foreach (var decl in _infrastructure)
                    {
                        var envVarName = e.Options.InfrastructureEnvVarNames.GetValueOrDefault(decl.Key)
                            ?? decl.DefaultEnvVarName;
                        envVars[envVarName] = decl.ConnectionString;
                    }

                    if (wireMockUrl is not null)
                    {
                        var wireMockEnvVarName = e.Options.InfrastructureEnvVarNames.GetValueOrDefault(WireMockOptions.InfrastructureKey)
                            ?? _wireMockOptions!.EnvVarName;
                        envVars[wireMockEnvVarName] = wireMockUrl;
                    }

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
                wireMock,
                infraContainers,
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

            foreach (var c in infraContainers)
                try { await c.DisposeAsync(); } catch { }

            if (network is not null)
                try { await network.DeleteAsync(); } catch { }

            throw;
        }
    }
}
