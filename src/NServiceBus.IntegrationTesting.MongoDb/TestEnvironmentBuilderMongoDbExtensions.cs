using Testcontainers.MongoDb;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Extends <see cref="TestEnvironmentBuilder"/> with MongoDB infrastructure support.
/// </summary>
public static class TestEnvironmentBuilderMongoDbExtensions
{
    /// <summary>
    /// Adds a MongoDB container to the environment. All endpoint containers receive a
    /// connection string environment variable pointing to it via the Docker network.
    /// Use the optional <paramref name="configure"/> callback to override the Docker image,
    /// the environment variable name, or set a custom <see cref="MongoDbContainerOptions.Key"/>
    /// and <see cref="MongoDbContainerOptions.NetworkAlias"/> to register multiple instances.
    /// Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using
    /// <see cref="MongoDbContainerOptions.Key"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseMongoDB(
        this TestEnvironmentBuilder builder,
        Action<MongoDbContainerOptions>? configure = null)
    {
        var opts = new MongoDbContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new MongoDbBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .Build(),
            $"mongodb://{opts.NetworkAlias}:27017");
    }
}
