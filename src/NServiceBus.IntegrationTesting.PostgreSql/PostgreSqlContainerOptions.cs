namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the PostgreSQL container added via
/// <see cref="TestEnvironmentBuilderPostgreSqlExtensions.UsePostgreSql"/>.
/// </summary>
public sealed class PostgreSqlContainerOptions
{
    /// <summary>
    /// The canonical key used to identify PostgreSQL infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "postgresql";

    /// <summary>
    /// The Docker image to use. Defaults to <c>postgres:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "postgres:latest";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>POSTGRESQL_CONNECTION_STRING</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "POSTGRESQL_CONNECTION_STRING";
}
