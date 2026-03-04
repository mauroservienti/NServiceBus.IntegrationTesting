using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
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
    readonly List<ContainerRegistration> _containers = [];

    record InfrastructureDeclaration(
        string Key,
        string DefaultEnvVarName,
        Func<INetwork, IContainer> BuildContainer,
        string ConnectionString);

    record EndpointRegistration(string EndpointName, string Dockerfile, EndpointContainerOptions Options,
        Func<ContainerBuilder, ContainerBuilder>? ContainerBuilderCallback);

    record ContainerRegistration(string Name, string Dockerfile, EndpointContainerOptions Options,
        Func<ContainerBuilder, ContainerBuilder>? ContainerBuilderCallback);

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
    /// Locates the repository root by walking up from <see cref="AppContext.BaseDirectory"/>
    /// until a directory named <paramref name="markerDirectory"/> is found, then returns
    /// that root (or the root joined with <paramref name="subPath"/> when provided).
    /// Throws <see cref="DirectoryNotFoundException"/> if the marker is never found.
    /// </summary>
    /// <param name="markerDirectory">Name of the directory to look for, e.g. <c>".git"</c>.</param>
    /// <param name="subPath">Optional relative path to append to the found root, e.g. <c>"src"</c>.</param>
    public static string FindRootByDirectory(string markerDirectory, string? subPath = null)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (current.GetDirectories(markerDirectory).Length > 0)
            {
                var root = current.FullName;
                return subPath is null ? root : Path.Combine(root, subPath);
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not find a directory named '{markerDirectory}' in '{AppContext.BaseDirectory}' or any of its parents.");
    }

    /// <summary>
    /// Locates the repository root by walking up from <see cref="AppContext.BaseDirectory"/>
    /// until a file matching <paramref name="filePattern"/> is found, then returns
    /// that root (or the root joined with <paramref name="subPath"/> when provided).
    /// Throws <see cref="FileNotFoundException"/> if the marker is never found.
    /// </summary>
    /// <param name="filePattern">File name or glob pattern to search for, e.g. <c>"*.sln"</c>.</param>
    /// <param name="subPath">Optional relative path to append to the found root, e.g. <c>"src"</c>.</param>
    public static string FindRootByFile(string filePattern, string? subPath = null)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (current.GetFiles(filePattern).Length > 0)
            {
                var root = current.FullName;
                return subPath is null ? root : Path.Combine(root, subPath);
            }
            current = current.Parent;
        }
        throw new FileNotFoundException(
            $"Could not find a file matching '{filePattern}' in '{AppContext.BaseDirectory}' or any of its parents.");
    }

    /// <summary>
    /// Registers an endpoint to run as a Docker container.
    /// <paramref name="dockerfile"/> is the path to the Dockerfile relative to the
    /// directory set via WithDockerfileDirectory.
    /// Use the optional <paramref name="containerOptions"/> callback to override per-endpoint
    /// environment variable names via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> or to inject
    /// additional static environment variables.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to
    /// expose ports via <c>b.WithPortBinding(port, assignRandomHostPort: true)</c>, add volume
    /// mounts, or set custom wait strategies. Retrieve the mapped host port after
    /// <see cref="StartAsync"/> via <see cref="EndpointHandle.GetMappedPort"/> or
    /// <see cref="EndpointHandle.GetBaseUrl"/>.
    /// Because Testcontainers builders are immutable, the callback must return the result of
    /// the chain.
    /// </summary>
    public TestEnvironmentBuilder AddEndpoint(string endpointName, string dockerfile,
        Action<EndpointContainerOptions>? containerOptions = null,
        Func<ContainerBuilder, ContainerBuilder>? containerBuilder = null)
    {
        if (_endpoints.Any(e => e.EndpointName == endpointName) ||
            _containers.Any(c => c.Name == endpointName))
            throw new ArgumentException(
                $"An endpoint named '{endpointName}' has already been added.", nameof(endpointName));

        var options = new EndpointContainerOptions();
        containerOptions?.Invoke(options);
        _endpoints.Add(new EndpointRegistration(endpointName, dockerfile, options, containerBuilder));
        return this;
    }

    /// <summary>
    /// Registers a container that participates in the test environment (same Docker network,
    /// same lifecycle) but does not host an NServiceBus agent. Use this for non-NServiceBus
    /// services such as ASP.NET Web APIs, sidecars, or other dependencies that your endpoints
    /// communicate with during tests. Access the running container via
    /// <see cref="TestEnvironment.GetContainer"/>.
    /// <para>
    /// Unlike <see cref="AddEndpoint"/>, no agent-connection wait is performed for containers
    /// registered with this method.
    /// </para>
    /// </summary>
    public TestEnvironmentBuilder AddContainer(string name, string dockerfile,
        Action<EndpointContainerOptions>? containerOptions = null,
        Func<ContainerBuilder, ContainerBuilder>? containerBuilder = null)
    {
        if (_containers.Any(c => c.Name == name) ||
            _endpoints.Any(e => e.EndpointName == name))
            throw new ArgumentException(
                $"A container named '{name}' has already been added.", nameof(name));

        var options = new EndpointContainerOptions();
        containerOptions?.Invoke(options);
        _containers.Add(new ContainerRegistration(name, dockerfile, options, containerBuilder));
        return this;
    }

    /// <summary>
    /// Starts an embedded WireMock.Net server in the test process. All endpoint containers
    /// receive the WireMock URL as an environment variable (default name: <c>WIREMOCK_URL</c>).
    /// Use the optional <paramref name="wireMockOptions"/> callback to override the variable name.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="WireMockOptions.InfrastructureKey"/>.
    /// Access the server via <see cref="TestEnvironment.WireMock"/>.
    /// </summary>
    public TestEnvironmentBuilder UseWireMock(Action<WireMockOptions>? wireMockOptions = null)
    {
        _wireMockOptions = new WireMockOptions();
        wireMockOptions?.Invoke(_wireMockOptions);
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
        List<(string Name, bool HasAgent, IContainer Container)> containerEntries = [];

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
                .Select(ep => (Name: ep.EndpointName, ep.Options, ep.ContainerBuilderCallback,
                    HasAgent: true, ep.Dockerfile))
                .Concat(_containers
                    .Select(c => (Name: c.Name, c.Options, c.ContainerBuilderCallback,
                        HasAgent: false, c.Dockerfile)))
                .Select(r => (r.Name, r.Options, r.ContainerBuilderCallback, r.HasAgent,
                    Image: new ImageFromDockerfileBuilder()
                        .WithDockerfileDirectory(_dockerfileDirectory)
                        .WithDockerfile(r.Dockerfile)
                        .WithName($"localhost/nsb-integration-testing/{r.Name.ToLowerInvariant()}:latest")
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

                    var cb = new ContainerBuilder(e.Image.FullName)
                        .WithNetwork(network)
                        .WithEnvironment(envVars)
                        .WithExtraHost("host.docker.internal", "host-gateway");
                    return (e.Name, e.HasAgent,
                        Container: (IContainer)(e.ContainerBuilderCallback?.Invoke(cb) ?? cb).Build());
                })
                .ToList();

            await Task.WhenAll(containerEntries.Select(e => e.Container.StartAsync(cancellationToken)));

            // ── Wait for all agents to connect ───────────────────────────────────
            using var agentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            agentCts.CancelAfter(_agentConnectionTimeout);

            var agentWaitTasks = _endpoints
                .Select(async ep =>
                {
                    bool connected;
                    try
                    {
                        await testHost.GrpcService.WaitForAgentAsync(ep.EndpointName, agentCts.Token);
                        connected = true;
                    }
                    catch (OperationCanceledException)
                    {
                        connected = false;
                    }
                    return (ep.EndpointName, connected);
                })
                .ToList();

            var agentResults = await Task.WhenAll(agentWaitTasks);
            var notConnected = agentResults
                .Where(r => !r.connected)
                .Select(r => r.EndpointName)
                .ToList();

            if (notConnected.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException(
                    $"The following endpoints did not connect within the {_agentConnectionTimeout} timeout: " +
                    $"{string.Join(", ", notConnected.Select(n => $"'{n}'"))}. " +
                    "If any of these containers do not host an NServiceBus agent, " +
                    "use AddContainer() instead of AddEndpoint().");
            }

            return new TestEnvironment(
                testHost,
                network,
                wireMock,
                infraContainers,
                containerEntries.Where(e => e.HasAgent).ToDictionary(e => e.Name, e => e.Container),
                containerEntries.Where(e => !e.HasAgent).ToDictionary(e => e.Name, e => e.Container));
        }
        catch
        {
            // Best-effort cleanup: attempt each disposal independently so a failure in
            // one step does not prevent the remaining resources from being released.
            foreach (var (_, _, container) in containerEntries)
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
