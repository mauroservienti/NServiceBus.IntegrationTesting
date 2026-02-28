namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the PostgreSQL container added via
/// <see cref="TestEnvironmentBuilder.UsePostgreSql"/>.
/// </summary>
public sealed class PostgreSqlContainerOptions
{
    /// <summary>
    /// The Docker image to use. Defaults to <c>postgres:15.1</c>.
    /// </summary>
    public string ImageName { get; set; } = "postgres:15.1";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>POSTGRESQL_CONNECTION_STRING</c>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "POSTGRESQL_CONNECTION_STRING";
}
