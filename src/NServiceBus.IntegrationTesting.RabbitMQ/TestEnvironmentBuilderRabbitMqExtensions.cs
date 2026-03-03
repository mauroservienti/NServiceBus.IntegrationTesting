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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="RabbitMqContainerOptions.Key"/>
    /// and <see cref="RabbitMqContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="RabbitMqContainerOptions.Key"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseRabbitMQ(
        this TestEnvironmentBuilder builder,
        Action<RabbitMqContainerOptions>? configure = null)
    {
        var opts = new RabbitMqContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new RabbitMqBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .Build(),
            $"host={opts.NetworkAlias};username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}");
    }
}
