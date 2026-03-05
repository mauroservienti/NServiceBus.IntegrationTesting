# Infrastructure Extensibility

`TestEnvironmentBuilder` ships with built-in support for infrastructure containers, each in
its own optional NuGet package:

| Package | Method | Default env var |
|---|---|---|
| `NServiceBus.IntegrationTesting.RabbitMQ` | `UseRabbitMQ()` | `RABBITMQ_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.PostgreSql` | `UsePostgreSql()` | `POSTGRESQL_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.MySql` | `UseMySQL()` | `MYSQL_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.SqlServer` | `UseSqlServer()` | `SQLSERVER_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.MongoDb` | `UseMongoDB()` | `MONGODB_CONNECTION_STRING` |
| `NServiceBus.IntegrationTesting.RavenDb` | `UseRavenDB()` | `RAVENDB_CONNECTION_STRING` |

All are extension methods on `TestEnvironmentBuilder`. You only need to reference the packages
that match your stack; the core `NServiceBus.IntegrationTesting` package has no dependency on
any Testcontainers module.

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
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L139-L152' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-use-infrastructure' title='Start of snippet'>anchor</a></sup>
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
        Action<RedisOptions>? containerOptions = null,
        Func<ContainerBuilder, ContainerBuilder>? containerBuilder = null)
    {
        var opts = new RedisOptions();
        containerOptions?.Invoke(opts);
        return builder.UseInfrastructure(
            opts.Key,
            opts.ConnectionStringEnvVarName,
            network =>
            {
                var builder = new ContainerBuilder(opts.ImageName)
                    .WithNetwork(network)
                    .WithNetworkAliases(opts.NetworkAlias);
                return (containerBuilder?.Invoke(builder) ?? builder).Build();
            },
            $"{opts.NetworkAlias}:6379");
    }
}
```
<sup><a href='/src/Snippets/InfrastructureExtensibilitySnippets.cs#L7-L77' title='Snippet source file'>snippet source</a> | <a href='#snippet-infra-extension-class' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Snippets/InfrastructureExtensibilitySnippets.cs#L85-L91' title='Snippet source file'>snippet source</a> | <a href='#snippet-infra-extension-usage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `Key` instance property on the options class is what endpoints reference in
`InfrastructureEnvVarNames` to override the env var name on a per-endpoint basis. The static
`InfrastructureKey` exposes the canonical default value and is useful when you haven't changed
`Key`.

### Escape hatch: full container customization

When `containerOptions` doesn't expose everything you need — custom volumes, extra environment
variables, non-standard wait strategies, additional labels — pass a `containerBuilder` callback
as the second argument. It receives the pre-configured builder and must return the modified one:

```csharp
.UseRedis(
    containerBuilder: b => b
        .WithEnvironment("REDIS_PASSWORD", "secret")
        .WithLabel("env", "test"))
```

Because Testcontainers builders are immutable, each `With*` call returns a new instance. The
callback must return the result of the chain — not just call methods on the input.

The same `containerBuilder` escape hatch is available on `AddEndpoint`. The primary use case
there is exposing container ports so the test process can query the endpoint over HTTP:

```csharp
.AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile",
    containerBuilder: b => b.WithPortBinding(8080, assignRandomHostPort: true))
```

Retrieve the mapped host port after `StartAsync` via `EndpointHandle`:

```csharp
var baseUrl = _env.GetEndpoint("MyEndpoint").GetBaseUrl(8080);
// e.g. "http://localhost:52341"
var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
```

Always use `assignRandomHostPort: true` — fixed host ports cause conflicts when tests run in
parallel across multiple suites or CI jobs.

`ConnectionStringEnvVarName` is auto-derived from `Key` when left unset (e.g. key `"redis"` →
`REDIS_CONNECTION_STRING`). Set it explicitly only when you need a name that doesn't follow that
convention.

`NetworkAlias` is the DNS name other containers on the Docker network use to reach this one.
It appears as the hostname in the injected connection string value.

### Multiple instances of the same type

Because `Key` and `NetworkAlias` are settable, you can register more than one container of
the same type. Give each a distinct `Key` and `NetworkAlias`:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UsePostgreSql(opts =>
    {
        opts.Key = "postgresql-primary";
        opts.NetworkAlias = "postgres-primary";
    })
    .UsePostgreSql(opts =>
    {
        opts.Key = "postgresql-replica";
        opts.NetworkAlias = "postgres-replica";
    })
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```

Each instance gets its own env var (auto-derived: `POSTGRESQL_PRIMARY_CONNECTION_STRING` and
`POSTGRESQL_REPLICA_CONNECTION_STRING`) and is reachable at its own Docker network hostname.
