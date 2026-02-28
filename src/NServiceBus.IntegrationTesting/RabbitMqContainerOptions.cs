namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the RabbitMQ container added via
/// <see cref="TestEnvironmentBuilder.UseRabbitMQ"/>.
/// </summary>
public sealed class RabbitMqContainerOptions
{
    /// <summary>
    /// The canonical key used to identify RabbitMQ infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "rabbitmq";

    /// <summary>
    /// The Docker image to use. Defaults to <c>rabbitmq:management</c>.
    /// </summary>
    public string ImageName { get; set; } = "rabbitmq:management";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>RABBITMQ_CONNECTION_STRING</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "RABBITMQ_CONNECTION_STRING";
}
