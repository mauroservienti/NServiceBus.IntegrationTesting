namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the RabbitMQ container added via
/// <see cref="TestEnvironmentBuilder.UseRabbitMQ"/>.
/// </summary>
public sealed class RabbitMqContainerOptions
{
    /// <summary>
    /// The Docker image to use. Defaults to <c>rabbitmq:management</c>.
    /// </summary>
    public string ImageName { get; set; } = "rabbitmq:management";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>RABBITMQ_CONNECTION_STRING</c>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "RABBITMQ_CONNECTION_STRING";
}
