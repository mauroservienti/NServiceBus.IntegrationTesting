using DotNet.Testcontainers.Builders;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with RavenDB infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderRavenDbExtensions
{
    /// <summary>
    /// Adds a RavenDB container to the environment. All endpoint containers receive an
    /// environment variable with the RavenDB server URL pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>RAVENDB_URL</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="RavenDbContainerOptions.InfrastructureKey"/>.
    /// <para>
    /// The container runs with <c>--Setup.Mode=None</c> so no setup wizard is required.
    /// The injected value is an HTTP URL: <c>http://ravendb:8080</c>.
    /// </para>
    /// </summary>
    public static TestEnvironmentBuilder UseRavenDB(
        this TestEnvironmentBuilder builder,
        Action<RavenDbContainerOptions>? configure = null)
    {
        var opts = new RavenDbContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            RavenDbContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new ContainerBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("ravendb")
                .WithEnvironment("RAVEN_ARGS", $"--Setup.Mode=None --ServerUrl=http://0.0.0.0:{opts.Port}")
                .WithExposedPort(opts.Port)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(opts.Port))
                .Build(),
            $"http://ravendb:{opts.Port}");
    }
}
