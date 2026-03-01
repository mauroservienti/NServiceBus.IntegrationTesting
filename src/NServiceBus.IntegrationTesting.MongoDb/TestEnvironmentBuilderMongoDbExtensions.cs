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
    /// Use the optional <paramref name="configure"/> callback to override the Docker image
    /// or the global default environment variable name
    /// (default: <c>MONGODB_CONNECTION_STRING</c>). Per-endpoint overrides are set via
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> using the key
    /// <see cref="MongoDbContainerOptions.InfrastructureKey"/>.
    /// </summary>
    public static TestEnvironmentBuilder UseMongoDB(
        this TestEnvironmentBuilder builder,
        Action<MongoDbContainerOptions>? configure = null)
    {
        var opts = new MongoDbContainerOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            MongoDbContainerOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new MongoDbBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("mongodb")
                .Build(),
            "mongodb://mongodb:27017");
    }
}
