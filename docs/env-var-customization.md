# Customizing Environment Variable Names

By default, NServiceBus.IntegrationTesting injects connection strings and infrastructure URLs
into endpoint containers using a fixed set of environment variable names:

| Infrastructure | Default env var name |
|---|---|
| RabbitMQ connection string | `RABBITMQ_CONNECTION_STRING` |
| PostgreSQL connection string | `POSTGRESQL_CONNECTION_STRING` |
| WireMock URL | `WIREMOCK_URL` |

In practice, endpoints owned by different teams often follow different naming conventions. One
team's endpoint might read `RABBIT_CONNECTION_STRING`, another might read
`TRANSPORT_CONNECTION_STRING`. The framework supports two levels of customization: a **global
default** for all endpoints, and a **per-endpoint override** for individual containers.

## Global defaults

Pass a configuration callback to `UseRabbitMQ()` or `UsePostgreSql()` to change the env var
name injected into every endpoint container:

<!-- snippet: env-var-global-rabbitmq -->
<a id='snippet-env-var-global-rabbitmq'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ(opts => opts.ConnectionStringEnvVarName = "TRANSPORT_CONNECTION_STRING")
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L11-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-global-rabbitmq' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L24-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-global-postgresql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> Both callbacks are optional. Calling `UseRabbitMQ()` or `UsePostgreSql()` with no arguments
> continues to use the default names shown in the table above.

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
    .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile",
        opts => opts.RabbitMqConnectionStringEnvVarName = "RABBIT_CONNECTION_STRING")
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile",
        opts => opts.RabbitMqConnectionStringEnvVarName = "TRANSPORT_CONNECTION_STRING")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L37-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-rabbitmq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### WireMock URL

<!-- snippet: env-var-per-endpoint-wiremock -->
<a id='snippet-env-var-per-endpoint-wiremock'></a>
```cs
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UseWireMock()
    .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile",
        opts => opts.WireMockUrlEnvVarName = "EXTERNAL_HTTP_STUB_URL")
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile",
        opts => opts.WireMockUrlEnvVarName = "MOCK_SERVER_URL")
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L53-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-wiremock' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L70-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-additional' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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
        opts.RabbitMqConnectionStringEnvVarName = "RABBIT_CONNECTION_STRING";
        opts.PostgreSqlConnectionStringEnvVarName = "DB_CONNECTION_STRING";
        opts.WireMockUrlEnvVarName = "EXTERNAL_HTTP_STUB_URL";
        opts.EnvironmentVariables["FEATURE_FLAG_X"] = "true";
    })
    .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
    {
        opts.RabbitMqConnectionStringEnvVarName = "TRANSPORT_CONNECTION_STRING";
        opts.WireMockUrlEnvVarName = "MOCK_SERVER_URL";
    })
    .StartAsync();
```
<sup><a href='/src/Snippets/EnvVarCustomizationSnippets.cs#L87-L106' title='Snippet source file'>snippet source</a> | <a href='#snippet-env-var-per-endpoint-combined' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Resolution order

For each endpoint container, the env var name is resolved as follows:

1. **Per-endpoint override** — the value set on `EndpointContainerOptions` (if non-`null`)
2. **Global default** — the value set on `RabbitMqContainerOptions` / `PostgreSqlContainerOptions`
3. **Built-in default** — the names in the table at the top of this page

The connection string *value* (i.e., the actual address and credentials) is always computed by
the framework from the running container; only the *name* of the environment variable is
customizable.
