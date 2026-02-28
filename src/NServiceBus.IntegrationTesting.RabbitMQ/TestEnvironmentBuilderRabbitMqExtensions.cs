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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>RABBITMQ_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="RabbitMqContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseRabbitMQ(
        this TestEnvironmentBuilder builder,
        Action<RabbitMqContainerOptions>? configure = null)
    {
        var opts = new RabbitMqContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            RabbitMqContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new RabbitMqBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("rabbitmq")
                .Build(),
            $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}");
    }
}
