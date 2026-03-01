namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the MongoDB container added via
/// <see cref="TestEnvironmentBuilderMongoDbExtensions.UseMongoDB"/>.
/// </summary>
public sealed class MongoDbContainerOptions
{
    /// <summary>
    /// The canonical key used to identify MongoDB infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "mongodb";

    /// <summary>
    /// The Docker image to use. Defaults to <c>mongo:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "mongo:latest";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <c>MONGODB_CONNECTION_STRING</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "MONGODB_CONNECTION_STRING";
}
