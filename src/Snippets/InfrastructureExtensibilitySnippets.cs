using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using NServiceBus.IntegrationTesting;

namespace Snippets.InfrastructureExtensibility;

// begin-snippet: infra-extension-class
public sealed class RedisOptions
{
    public static string InfrastructureKey => "redis";

    string _key = InfrastructureKey;
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

    public string ImageName { get; set; } = "redis:7";

    string? _connectionStringEnvVarName;
    public string ConnectionStringEnvVarName
    {
        get => _connectionStringEnvVarName
            ?? Key.Replace("-", "_").ToUpperInvariant() + "_CONNECTION_STRING";
        set => _connectionStringEnvVarName = value;
    }
}

public static class TestEnvironmentBuilderRedisExtensions
{
    public static TestEnvironmentBuilder UseRedis(
        this TestEnvironmentBuilder builder,
        Action<RedisOptions>? configure = null)
    {
        var opts = new RedisOptions();
        configure?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network => new ContainerBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases(opts.NetworkAlias)
                .Build(),
            $"{opts.NetworkAlias}:6379");
    }
}
// end-snippet

public class InfrastructureExtensibilitySnippets
{
    public async Task UseExtension()
    {
        string srcDir = null!;

        // begin-snippet: infra-extension-usage
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRedis()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    static TestEnvironment _env = null!;
}
