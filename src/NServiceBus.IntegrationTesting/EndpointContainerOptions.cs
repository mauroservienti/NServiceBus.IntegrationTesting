namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Per-endpoint container options configurable via
/// <see cref="TestEnvironmentBuilder.AddEndpoint(string, string, Action{EndpointContainerOptions}?)"/>.
/// </summary>
public sealed class EndpointContainerOptions
{
    /// <summary>
    /// Overrides the environment variable name used to inject the RabbitMQ connection string
    /// into this endpoint's container. When <c>null</c>, the name configured on
    /// <see cref="RabbitMqContainerOptions.ConnectionStringEnvVarName"/> is used.
    /// </summary>
    public string? RabbitMqConnectionStringEnvVarName { get; set; }

    /// <summary>
    /// Overrides the environment variable name used to inject the PostgreSQL connection string
    /// into this endpoint's container. When <c>null</c>, the name configured on
    /// <see cref="PostgreSqlContainerOptions.ConnectionStringEnvVarName"/> is used.
    /// </summary>
    public string? PostgreSqlConnectionStringEnvVarName { get; set; }

    /// <summary>
    /// Overrides the environment variable name used to inject the WireMock URL into this
    /// endpoint's container. When <c>null</c>, <c>WIREMOCK_URL</c> is used.
    /// </summary>
    public string? WireMockUrlEnvVarName { get; set; }

    /// <summary>
    /// Additional static environment variables to inject into this endpoint's container.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
}
