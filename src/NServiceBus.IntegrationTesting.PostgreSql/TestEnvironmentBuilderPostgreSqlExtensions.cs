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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="PostgreSqlContainerOptions.Key"/>
    /// and <see cref="PostgreSqlContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="PostgreSqlContainerOptions.Key"/>.
    /// </summary>
    public static TestEnvironmentBuilder UsePostgreSql(
        this TestEnvironmentBuilder builder,
        Action<PostgreSqlContainerOptions>? configure = null)
    {
        var opts = new PostgreSqlContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new PostgreSqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .Build(),
            $"Host={opts.NetworkAlias};Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
            $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}");
    }
}
