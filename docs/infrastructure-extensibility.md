# Infrastructure Extensibility

`TestEnvironmentBuilder` ships with built-in support for two infrastructure containers, each in
its own optional NuGet package:

| Package | Method | Default env var |
|---|---|---|
| `NServiceBus.IntegrationTesting.RabbitMQ` | `UseRabbitMQ()` | `RABBITMQ_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.PostgreSql` | `UsePostgreSql()` | `POSTGRESQL_CONNECTION_STRING` |

Both are extension methods on `TestEnvironmentBuilder`. You only need to reference the packages
that match your stack; the core `NServiceBus.IntegrationTesting` package has no dependency on
either Testcontainers module.

## Adding any other infrastructure (one-off)

For infrastructure not covered by a built-in package, call `UseInfrastructure()` directly.
It accepts any `IContainer` produced by Testcontainers and injects the connection string into
every endpoint container under the variable name you choose:

<!-- snippet: env-var-use-infrastructure -->
<a id='snippet-env-var-use-infrastructure'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseInfrastructure(
        key: "redis",
        defaultEnvVarName: "REDIS_CONNECTION_STRING",
        buildContainer: network => new ContainerBuilder("redis:7")
            .WithNetwork(network)
            .WithNetworkAliases("redis")
            .Build(),
        connectionString: "redis:6379")
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L116-L129' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-use-infrastructure' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Parameters:

- `key` — a stable string identifier for this infrastructure piece. Endpoints use it as the dictionary key in `InfrastructureEnvVarNames` to override the env var name on a per-endpoint basis (see [Customizing Environment Variable Names](env-var-customization.md)).
- `defaultEnvVarName` — the environment variable name injected into every endpoint container unless overridden.
- `buildContainer` — a factory that receives the shared Docker network and returns the built `IContainer`. Attach the container to the network and give it a DNS alias so endpoints can reach it by hostname.
- `connectionString` — the **string value** injected as the env var. This is whatever your endpoint reads and interprets — not something the framework parses. For a Redis container accessible at hostname `redis` on port `6379`, the value is `"redis:6379"`. Use the Docker network alias as the hostname, not `localhost`.

## Creating a reusable extension method

If the same custom infrastructure appears across multiple test projects, extract it into a
static extension method following the same pattern as the built-in packages. A dedicated
options class makes the image name and env var name configurable without coupling callers to
`UseInfrastructure` details:

<!-- snippet: infra-extension-class -->
<a id='snippet-infra-extension-class'></a>
```cs
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
            network => new ContainerBuilder(opts.ImageName)
                .WithNetwork(network)
                .WithNetworkAliases("redis")
                .Build(),
            "redis:6379");
    }
}
```
<sup><a href='/src/Snippets/InfrastructureExtensibilitySnippets.cs#L7-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-infra-extension-class' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Callers see the same fluent API as the built-in packages:

<!-- snippet: infra-extension-usage -->
<a id='snippet-infra-extension-usage'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRedis()
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```
<sup><a href='/src/Snippets/InfrastructureExtensibilitySnippets.cs#L41-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-infra-extension-usage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `InfrastructureKey` property on the options class is what endpoints reference in
`InfrastructureEnvVarNames` to override the env var name on a per-endpoint basis.
