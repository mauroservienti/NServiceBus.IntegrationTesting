using Testcontainers.RabbitMq;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with RabbitMQ infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderRabbitMqExtensions
{
    /// <summary>
    /// Adds a RabbitMQ container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="containerOptions"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="RabbitMqContainerOptions.Key"/>
    /// and <see cref="RabbitMqContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="RabbitMqContainerOptions.Key"/>.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to add
    /// volume mounts, extra environment variables, or custom wait strategies.
    /// </summary>
    public static TestEnvironmentBuilder UseRabbitMQ(
        this TestEnvironmentBuilder builder,
        Action<RabbitMqContainerOptions>? containerOptions = null,
        Func<RabbitMqBuilder, RabbitMqBuilder>? containerBuilder = null)
    {
        var opts = new RabbitMqContainerOptions();
        containerOptions?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var cb = new RabbitMqBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias);
                return (containerBuilder?.Invoke(cb) ?? cb).Build();
            },
            $"host={opts.NetworkAlias};username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}");
    }
}
