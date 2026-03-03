using Testcontainers.PostgreSql;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with PostgreSQL infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderPostgreSqlExtensions
{
    /// <summary>
    /// Adds a PostgreSQL container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="containerOptions"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="PostgreSqlContainerOptions.Key"/>
    /// and <see cref="PostgreSqlContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="PostgreSqlContainerOptions.Key"/>.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to add
    /// volume mounts, extra environment variables, or custom wait strategies.
    /// </summary>
    public static TestEnvironmentBuilder UsePostgreSql(
        this TestEnvironmentBuilder builder,
        Action<PostgreSqlContainerOptions>? containerOptions = null,
        Func<PostgreSqlBuilder, PostgreSqlBuilder>? containerBuilder = null)
    {
        var opts = new PostgreSqlContainerOptions();
        containerOptions?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var builder = new PostgreSqlBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias);
                return (containerBuilder?.Invoke(builder) ?? builder).Build();
            },
            $"Host={opts.NetworkAlias};Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
            $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}");
    }
}
