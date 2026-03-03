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
    /// Use the optional <paramref name="containerOptions"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="RavenDbContainerOptions.Key"/>
    /// and <see cref="RavenDbContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="RavenDbContainerOptions.Key"/>.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to add
    /// volume mounts, extra environment variables, or custom wait strategies.
    /// <para>
    /// The container runs with <c>--Setup.Mode=None</c> so no setup wizard is required.
    /// The injected value is an HTTP URL: <c>http://ravendb:8080</c>.
    /// </para>
    /// </summary>
    public static TestEnvironmentBuilder UseRavenDB(
        this TestEnvironmentBuilder builder,
        Action<RavenDbContainerOptions>? containerOptions = null,
        Func<ContainerBuilder, ContainerBuilder>? containerBuilder = null)
    {
        var opts = new RavenDbContainerOptions();
        containerOptions?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var builder = new ContainerBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias)
                    .WithEnvironment("RAVEN_ARGS", $"--Setup.Mode=None --ServerUrl=http://0.0.0.0:{opts.Port}")
                    .WithExposedPort(opts.Port)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(opts.Port));
                return (containerBuilder?.Invoke(builder) ?? builder).Build();
            },
            $"http://{opts.NetworkAlias}:{opts.Port}");
    }
}
