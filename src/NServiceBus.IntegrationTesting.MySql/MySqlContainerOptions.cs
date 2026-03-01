namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the MySQL container added via
/// <see cref="TestEnvironmentBuilderMySqlExtensions.UseMySQL"/>.
/// </summary>
public sealed class MySqlContainerOptions
{
    /// <summary>
    /// The canonical key used to identify MySQL infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "mysql";

    /// <summary>
    /// The Docker image to use. Defaults to <c>mysql:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "mysql:latest";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>MYSQL_CONNECTION_STRING</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "MYSQL_CONNECTION_STRING";
}
