using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using NServiceBus.IntegrationTesting;

namespace Snippets.InfrastructureExtensibility;

// begin-snippet: infra-extension-class
public sealed class RedisOptions
{
    public static string InfrastructureKey => "redis";
    public string ImageName { get; set; } = "redis:7";
    public string ConnectionStringEnvVarName { get; set; } = "REDIS_CONNECTION_STRING";
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
            RedisOptions.InfrastructureKey,
            opts.ConnectionStringEnvVarName,
            network => new ContainerBuilder()
                .WithImage(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("redis")
                .Build(),
            "redis:6379");
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
