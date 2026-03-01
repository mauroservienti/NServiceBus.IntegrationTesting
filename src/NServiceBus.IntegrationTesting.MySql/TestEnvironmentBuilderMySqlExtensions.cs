using Testcontainers.MySql;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with MySQL infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderMySqlExtensions
{
    /// <summary>
    /// Adds a MySQL container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>MYSQL_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="MySqlContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseMySQL(
        this TestEnvironmentBuilder builder,
        Action<MySqlContainerOptions>? configure = null)
    {
        var opts = new MySqlContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            MySqlContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new MySqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("mysql")
                .Build(),
            $"Server=mysql;Port=3306;Database={MySqlBuilder.DefaultDatabase}" +
            $";Uid={MySqlBuilder.DefaultUsername};Pwd={MySqlBuilder.DefaultPassword}");
    }
}
