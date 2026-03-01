namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the SQL Server container added via
/// <see cref="TestEnvironmentBuilderSqlServerExtensions.UseSqlServer"/>.
/// </summary>
public sealed class SqlServerContainerOptions
{
    /// <summary>
    /// The canonical key used to identify SQL Server infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "sqlserver";

    /// <summary>
    /// The Docker image to use. Defaults to <c>mcr.microsoft.com/mssql/server:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "mcr.microsoft.com/mssql/server:latest";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>SQLSERVER_CONNECTION_STRING</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "SQLSERVER_CONNECTION_STRING";
}
