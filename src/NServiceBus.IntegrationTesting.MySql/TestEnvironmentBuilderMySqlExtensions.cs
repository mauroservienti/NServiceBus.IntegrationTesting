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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="MySqlContainerOptions.Key"/>
    /// and <see cref="MySqlContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="MySqlContainerOptions.Key"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseMySQL(
        this TestEnvironmentBuilder builder,
        Action<MySqlContainerOptions>? configure = null)
    {
        var opts = new MySqlContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new MySqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .Build(),
            $"Server={opts.NetworkAlias};Port=3306;Database={MySqlBuilder.DefaultDatabase}" +
            $";Uid={MySqlBuilder.DefaultUsername};Pwd={MySqlBuilder.DefaultPassword}");
    }
}
