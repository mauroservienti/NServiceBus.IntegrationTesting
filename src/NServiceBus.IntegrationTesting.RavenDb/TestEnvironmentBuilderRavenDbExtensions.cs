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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="RavenDbContainerOptions.Key"/>
    /// and <see cref="RavenDbContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="RavenDbContainerOptions.Key"/>.
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
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new ContainerBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .WithEnvironment("RAVEN_ARGS", $"--Setup.Mode=None --ServerUrl=http://0.0.0.0:{opts.Port}")
                .WithExposedPort(opts.Port)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(opts.Port))
                .Build(),
            $"http://{opts.NetworkAlias}:{opts.Port}");
    }
}
