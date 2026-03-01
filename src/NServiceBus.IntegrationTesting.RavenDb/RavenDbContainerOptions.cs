namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the RavenDB container added via
/// <see cref="TestEnvironmentBuilderRavenDbExtensions.UseRavenDB"/>.
/// </summary>
public sealed class RavenDbContainerOptions
{
    /// <summary>
    /// The canonical key used to identify RavenDB infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "ravendb";

    /// <summary>
    /// The Docker image to use. Defaults to <c>ravendb/ravendb:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "ravendb/ravendb:latest";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// RavenDB server URL. Defaults to <c>RAVENDB_URL</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName { get; set; } = "RAVENDB_URL";

    /// <summary>
    /// The HTTP port RavenDB listens on inside the container. Defaults to <c>8080</c>.
    /// </summary>
    public int Port { get; set; } = 8080;
}
