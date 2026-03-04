# Customizing Environment Variable Names

By default, NServiceBus.IntegrationTesting injects connection strings and infrastructure URLs
into endpoint containers using a fixed set of environment variable names:

| Infrastructure | Default env var name |
|---|---|
| RabbitMQ connection string | `RABBITMQ_CONNECTION_STRING` |
| PostgreSQL connection string | `POSTGRESQL_CONNECTION_STRING` |
| MySQL connection string | `MYSQL_CONNECTION_STRING` |
| SQL Server connection string | `SQLSERVER_CONNECTION_STRING` |
| MongoDB connection string | `MONGODB_CONNECTION_STRING` |
| RavenDB connection string | `RAVENDB_CONNECTION_STRING` |
| WireMock URL | `WIREMOCK_URL` |

In practice, endpoints owned by different teams often follow different naming conventions. One
team's endpoint might read `RABBIT_CONNECTION_STRING`, another might read
`TRANSPORT_CONNECTION_STRING`. The framework supports two levels of customization: a **global
default** for all endpoints, and a **per-endpoint override** for individual containers.

## Global defaults

Pass a configuration callback to any `UseXxx()` method to change the env var name injected
into every endpoint container:

<!-- snippet: env-var-global-rabbitmq -->
<a id='snippet-env-var-global-rabbitmq'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ(opts => opts.ConnectionStringEnvVarName = "TRANSPORT_CONNECTION_STRING")
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L14-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-global-rabbitmq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: env-var-global-postgresql -->
<a id='snippet-env-var-global-postgresql'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UsePostgreSql(opts => opts.ConnectionStringEnvVarName = "DB_CONNECTION_STRING")
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L27-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-global-postgresql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> All callbacks are optional. Calling any `UseXxx()` with no arguments continues to use the
> default names shown in the table above.

## Per-endpoint overrides

When different endpoints use different naming conventions for the same infrastructure, supply a
configuration callback to `AddEndpoint()`. Any value set here takes precedence over the global
default for that endpoint only.

### RabbitMQ

<!-- snippet: env-var-per-endpoint-rabbitmq -->
<a id='snippet-env-var-per-endpoint-rabbitmq'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
        opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "RABBIT_CONNECTION_STRING")
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
        opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "TRANSPORT_CONNECTION_STRING")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L40-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-rabbitmq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### WireMock URL

<!-- snippet: env-var-per-endpoint-wiremock -->
<a id='snippet-env-var-per-endpoint-wiremock'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UseWireMock()
    .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
        opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "EXTERNAL_HTTP_STUB_URL")
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
        opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "MOCK_SERVER_URL")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L56-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-wiremock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Additional static environment variables

Inject arbitrary static values into a specific endpoint's container via the
`EnvironmentVariables` dictionary:

<!-- snippet: env-var-per-endpoint-additional -->
<a id='snippet-env-var-per-endpoint-additional'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile", opts =>
    {
        opts.EnvironmentVariables["FEATURE_FLAG_X"] = "true";
        opts.EnvironmentVariables["API_BASE_URL"] = "https://internal.example.com";
    })
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L73-L83' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-additional' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Setting a Docker network alias

By default, containers on the shared Docker network are only reachable by their random container ID. Set `NetworkAlias` to give a container a stable hostname that other containers on the same network can use:

<!-- snippet: env-var-network-alias -->
<a id='snippet-env-var-network-alias'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .AddEndpoint("WebApp", "WebApp.Testing/Dockerfile", opts =>
    {
        opts.NetworkAlias = "webapp";
    })
    .AddContainer("InventoryService", "InventoryService/Dockerfile", opts =>
    {
        opts.NetworkAlias = "inventory";
    })
    .StartAsync();

// Other containers can now reach "WebApp" at http://webapp:<port>
// and "InventoryService" at http://inventory:<port>
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L90-L106' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-network-alias' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This works for both `AddEndpoint` and `AddContainer`.

### Combining overrides

All per-endpoint options can be combined in a single callback:

<!-- snippet: env-var-per-endpoint-combined -->
<a id='snippet-env-var-per-endpoint-combined'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UsePostgreSql()
    .UseWireMock()
    .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
    {
        opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "RABBIT_CONNECTION_STRING";
        opts.InfrastructureEnvVarNames[PostgreSqlContainerOptions.InfrastructureKey] = "DB_CONNECTION_STRING";
        opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "EXTERNAL_HTTP_STUB_URL";
        opts.EnvironmentVariables["FEATURE_FLAG_X"] = "true";
    })
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
    {
        opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "TRANSPORT_CONNECTION_STRING";
        opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "MOCK_SERVER_URL";
    })
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L113-L132' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-combined' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Adding custom infrastructure with `UseInfrastructure`

All `UseXxx()` methods are convenience wrappers around the general-purpose
`UseInfrastructure()` method. You can use it directly to add any Testcontainers-managed
infrastructure — no framework changes needed:

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

The `key` you choose is what endpoints use in `InfrastructureEnvVarNames` to override the
env var name for that infrastructure.

## Resolution order

For each endpoint container, the env var name is resolved as follows:

1. **Per-endpoint override** — `InfrastructureEnvVarNames[key]` in `EndpointContainerOptions`
2. **Options default** — `ConnectionStringEnvVarName` on the infrastructure options class.
   When not set explicitly, this is auto-derived from `Key` by uppercasing it, replacing
   hyphens with underscores, and appending `_CONNECTION_STRING` (e.g. `Key = "postgresql"`
   → `POSTGRESQL_CONNECTION_STRING`).

The connection string *value* (i.e., the actual address and credentials) is always computed by
the framework; only the *name* of the environment variable is customizable.
