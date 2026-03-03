using DotNet.Testcontainers.Networks;
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
    /// Use the optional <paramref name="containerOptions"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="SqlServerContainerOptions.Key"/>
    /// and <see cref="SqlServerContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="SqlServerContainerOptions.Key"/>.
    /// Use the optional <paramref name="containerBuilder"/> callback to further customize the
    /// container beyond what <paramref name="containerOptions"/> supports — for example, to add
    /// volume mounts, extra environment variables, or custom wait strategies.
    /// </summary>
    public static TestEnvironmentBuilder UseSqlServer(
        this TestEnvironmentBuilder builder,
        Action<SqlServerContainerOptions>? containerOptions = null,
        Func<MsSqlBuilder, MsSqlBuilder>? containerBuilder = null)
    {
        var opts = new SqlServerContainerOptions();
        containerOptions?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var builder = new MsSqlBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias);
                
                return (containerBuilder?.Invoke(builder) ?? builder).Build();
            },
            $"Server={opts.NetworkAlias},1433;Database={MsSqlBuilder.DefaultDatabase}" +
            $";User Id=sa;Password={MsSqlBuilder.DefaultPassword};TrustServerCertificate=True");
    }
}
