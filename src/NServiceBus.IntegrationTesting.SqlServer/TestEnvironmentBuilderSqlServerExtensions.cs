using Testcontainers.MsSql;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with SQL Server infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderSqlServerExtensions
{
    /// <summary>
    /// Adds a SQL Server container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>SQLSERVER_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="SqlServerContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseSqlServer(
        this TestEnvironmentBuilder builder,
        Action<SqlServerContainerOptions>? configure = null)
    {
        var opts = new SqlServerContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            SqlServerContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new MsSqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("mssql")
                .Build(),
            $"Server=mssql,1433;Database={MsSqlBuilder.DefaultDatabase}" +
            $";User Id=sa;Password={MsSqlBuilder.DefaultPassword};TrustServerCertificate=True");
    }
}
