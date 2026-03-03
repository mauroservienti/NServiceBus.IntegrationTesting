namespace NServiceBus.IntegrationTesting;

/// <summary>
/// Configuration options for the MySQL container added via
/// <see cref="TestEnvironmentBuilderMySqlExtensions.UseMySQL"/>.
/// </summary>
public sealed class MySqlContainerOptions
{
    /// <summary>
    /// The canonical key used to identify MySQL infrastructure in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides.
    /// </summary>
    public static string InfrastructureKey => "mysql";

    string _key = InfrastructureKey;

    /// <summary>
    /// Unique key identifying this infrastructure instance. Used as the lookup key in
    /// <see cref="EndpointContainerOptions.InfrastructureEnvVarNames"/> overrides, the
    /// basis for the default <see cref="ConnectionStringEnvVarName"/>, and the default
    /// <see cref="NetworkAlias"/>. Defaults to <see cref="InfrastructureKey"/>. When
    /// registering multiple MySQL instances, set a distinct key for each.
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
    /// Defaults to <see cref="Key"/>. When registering multiple MySQL instances,
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
    /// The Docker image to use. Defaults to <c>mysql:latest</c>.
    /// </summary>
    public string ImageName { get; set; } = "mysql:latest";

    /// <summary>
    /// The database name. When <see langword="null"/>, the Testcontainers default
    /// (<c>mysqldb</c>) is used. The same resolved value is injected into the connection
    /// string and passed to <see cref="Testcontainers.MySql.MySqlBuilder.WithDatabase"/>.
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// The MySQL username. When <see langword="null"/>, the Testcontainers default
    /// (<c>root</c>) is used. The same resolved value is injected into the connection
    /// string and passed to <see cref="Testcontainers.MySql.MySqlBuilder.WithUsername"/>.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The MySQL password. When <see langword="null"/>, the Testcontainers default
    /// (<c>mysql</c>) is used. The same resolved value is injected into the connection
    /// string and passed to <see cref="Testcontainers.MySql.MySqlBuilder.WithPassword"/>.
    /// </summary>
    public string? Password { get; set; }

    string? _connectionStringEnvVarName;

    /// <summary>
    /// The environment variable name injected into all endpoint containers with the
    /// connection string value. Defaults to <see cref="Key"/> uppercased with hyphens
    /// replaced by underscores, suffixed with <c>_CONNECTION_STRING</c>
    /// (e.g. key <c>mysql</c> → <c>MYSQL_CONNECTION_STRING</c>).
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
