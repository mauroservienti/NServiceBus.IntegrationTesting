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

    string _key = InfrastructureKey;

    /// <summary>
    /// Unique key identifying this infrastructure instance. Used as the lookup key in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides, the
    /// basis for the default <see cref="ConnectionStringEnvVarName"/>, and the default
    /// <see cref="NetworkAlias"/>. Defaults to <see cref="InfrastructureKey"/>. When
    /// registering multiple MongoDB instances, set a distinct key for each.
    /// Must contain only lowercase letters, digits, and hyphens, and must not start or
    /// end with a hyphen.
    /// </summary>
    public string Key
    {
        get => _key;
        set
        {
            if (string.IsNullOrEmpty(value) ||
                !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') ||
                value[0] == '-' || value[^1] == '-')
                throw new ArgumentException(
                    $"'{value}' is not a valid key. Keys must contain only lowercase letters, digits, and hyphens, and must not start or end with a hyphen.",
                    nameof(value));
            _key = value;
        }
    }

    string? _networkAlias;

    /// <summary>
    /// Docker network alias assigned to the container. Other containers on the same
    /// network reach it using this name as the hostname in the connection string.
    /// Defaults to <see cref="Key"/>. When registering multiple MongoDB instances,
    /// set a distinct alias for each.
    /// Must contain only lowercase letters, digits, and hyphens, and must not start or
    /// end with a hyphen.
    /// </summary>
    public string NetworkAlias
    {
        get => _networkAlias ?? Key;
        set
        {
            if (string.IsNullOrEmpty(value) ||
                !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') ||
                value[0] == '-' || value[^1] == '-')
                throw new ArgumentException(
                    $"'{value}' is not a valid network alias. Aliases must contain only lowercase letters, digits, and hyphens, and must not start or end with a hyphen.",
                    nameof(value));
            _networkAlias = value;
        }
    }

    /// <summary>
    /// The Docker image to use. Defaults to <c>mongo:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "mongo:latest";

    string? _connectionStringEnvVarName;

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <see cref="Key"/> uppercased with hyphens
    /// replaced by underscores, suffixed with <c>_CONNECTION_STRING</c>
    /// (e.g. key <c>mongodb</c> → <c>MONGODB_CONNECTION_STRING</c>).
    /// Per-endpoint overrides take precedence; see
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/>.
    /// </summary>
    public string ConnectionStringEnvVarName
    {
        get => _connectionStringEnvVarName
            ?? Key.Replace("-", "_").ToUpperInvariant() + "_CONNECTION_STRING";
        set => _connectionStringEnvVarName = value;
    }
}
