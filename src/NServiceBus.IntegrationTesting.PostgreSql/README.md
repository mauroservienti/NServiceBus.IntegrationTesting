# NServiceBus.IntegrationTesting.PostgreSql

Adds a PostgreSQL container to [NServiceBus.IntegrationTesting](https://github.com/mauroservienti/NServiceBus.IntegrationTesting) test environments.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop/) installed and running

## Usage

Add this package to your test project alongside `NServiceBus.IntegrationTesting`, then call `.UsePostgreSql()` on `TestEnvironmentBuilder`:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UsePostgreSql()
    .AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile")
    .StartAsync();
```

The PostgreSQL container is joined to the shared Docker network and `TestEnvironmentBuilder` injects the `POSTGRESQL_CONNECTION_STRING` environment variable into every endpoint container. No Docker network wiring is needed on your side — your endpoint just reads the variable at startup:

```csharp
persistence.ConnectionBuilder(() =>
    new NpgsqlConnection(Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")));
```

The injected value is a standard **ADO.NET connection string**:

```
Host=postgres;Port=5432;Database=postgres;Username=postgres;Password=postgres
```

The hostname `postgres` is the container's name on the shared Docker network — endpoints reach it by that name without any extra configuration.

## Defaults

| Setting | Default |
|---|---|
| Environment variable | `POSTGRESQL_CONNECTION_STRING` |
| Docker image | `postgres:latest` |

## Customization

```csharp
.UsePostgreSql(opts =>
{
    opts.ConnectionStringEnvVarName = "MY_CUSTOM_VAR";
    opts.ImageName = "postgres:16";
})
```

For per-endpoint variable name overrides, see [Customizing Environment Variable Names](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/docs/env-var-customization.md).

## All packages

| Package | Purpose |
|---|---|
| `NServiceBus.IntegrationTesting` | Test host, `TestEnvironmentBuilder`, observe API |
| `NServiceBus.IntegrationTesting.RabbitMQ` | RabbitMQ transport container |
| `NServiceBus.IntegrationTesting.PostgreSql` | PostgreSQL persistence container |
| `NServiceBus.IntegrationTesting.MySql` | MySQL persistence container |
| `NServiceBus.IntegrationTesting.SqlServer` | SQL Server persistence container |
| `NServiceBus.IntegrationTesting.MongoDb` | MongoDB persistence container |
| `NServiceBus.IntegrationTesting.RavenDb` | RavenDB persistence container |
| `NServiceBus.IntegrationTesting.AgentV10` | Agent for NServiceBus 10 (net10.0) |
| `NServiceBus.IntegrationTesting.AgentV9` | Agent for NServiceBus 9 (net8.0) |
| `NServiceBus.IntegrationTesting.AgentV8` | Agent for NServiceBus 8 (net6.0) |

## Documentation

For the full walkthrough and API reference, see the **[getting started guide](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/docs/getting-started.md)**.
