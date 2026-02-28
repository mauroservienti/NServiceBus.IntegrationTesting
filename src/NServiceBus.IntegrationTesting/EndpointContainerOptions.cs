namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Per-endpoint container options configurable via
/// <see cref="TestEnvironmentBuilder.AddEndpoint(string, string, Action{EndpointContainerOptions}?)"/>.
/// </summary>
public sealed class EndpointContainerOptions
{
    /// <summary>
    /// Per-endpoint overrides for the environment variable name used to inject each
    /// infrastructure's connection string or URL. Keys are the canonical infrastructure
    /// keys (e.g. <see cref="RabbitMqContainerOptions.InfrastructureKey"/>,
    /// <see cref="PostgreSqlContainerOptions.InfrastructureKey"/>,
    /// <see cref="WireMockOptions.InfrastructureKey"/>). When a key is absent the global
    /// default configured on the corresponding options class is used.
    /// </summary>
    public Dictionary<string, string> InfrastructureEnvVarNames { get; } = [];

    /// <summary>
    /// Additional static environment variables to inject into this endpoint's container.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
}
