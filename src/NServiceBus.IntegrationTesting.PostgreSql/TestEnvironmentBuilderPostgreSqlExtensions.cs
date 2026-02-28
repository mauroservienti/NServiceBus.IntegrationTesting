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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>POSTGRESQL_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="PostgreSqlContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public static TestEnvironmentBuilder UsePostgreSql(
        this TestEnvironmentBuilder builder,
        Action<PostgreSqlContainerOptions>? configure = null)
    {
        var opts = new PostgreSqlContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            PostgreSqlContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new PostgreSqlBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("postgres")
                .Build(),
            $"Host=postgres;Port=5432;Database={PostgreSqlBuilder.DefaultDatabase}" +
            $";Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}");
    }
}
