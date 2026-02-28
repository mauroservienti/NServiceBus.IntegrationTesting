namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the WireMock stub server added via
/// <see cref="TestEnvironmentBuilder.UseWireMock"/>.
/// </summary>
public sealed class WireMockOptions
{
    /// <summary>
    /// The canonical key used to identify WireMock infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "wiremock";

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// WireMock server URL. Defaults to <c>WIREMOCK_URL</c>.
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string EnvVarName { get; set; } = "WIREMOCK_URL";
}
