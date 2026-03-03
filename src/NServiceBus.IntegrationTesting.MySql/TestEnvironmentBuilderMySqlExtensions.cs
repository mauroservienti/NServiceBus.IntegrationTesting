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
    /// Use the optional <paramref name="containerOptions"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="MySqlContainerOptions.Key"/>
    /// and <see cref="MySqlContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="MySqlContainerOptions.Key"/>.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to add
    /// volume mounts, extra environment variables, or custom wait strategies.
    /// </summary>
    public static TestEnvironmentBuilder UseMySQL(
        this TestEnvironmentBuilder builder,
        Action<MySqlContainerOptions>? containerOptions = null,
        Func<MySqlBuilder, MySqlBuilder>? containerBuilder = null)
    {
        var opts = new MySqlContainerOptions();
        containerOptions?.Invoke(opts);
        var database = opts.Database ?? MySqlBuilder.DefaultDatabase;
        var username = opts.Username ?? MySqlBuilder.DefaultUsername;
        var password = opts.Password ?? MySqlBuilder.DefaultPassword;
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var builder = new MySqlBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias)
                    .WithDatabase(database)
                    .WithUsername(username)
                    .WithPassword(password);
                return (containerBuilder?.Invoke(builder) ?? builder).Build();
            },
            $"Server={opts.NetworkAlias};Port=3306;Database={database}" +
            $";Uid={username};Pwd={password}");
    }
}
