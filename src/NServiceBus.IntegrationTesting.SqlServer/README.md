# NServiceBus.IntegrationTesting.SqlServer

Adds a SQL Server container to [NServiceBus.IntegrationTesting](https://github.com/mauroservienti/NServiceBus.IntegrationTesting) test environments.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop/) installed and running

## Usage

Add this package to your test project alongside `NServiceBus.IntegrationTesting`, then call `.UseSqlServer()` on `TestEnvironmentBuilder`:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UseSqlServer()
    .AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile")
    .StartAsync();
```

The SQL Server container is joined to the shared Docker network and `TestEnvironmentBuilder` injects the `SQLSERVER_CONNECTION_STRING` environment variable into every endpoint container. No Docker network wiring is needed on your side — your endpoint just reads the variable at startup:

```csharp
persistence.ConnectionBuilder(() =>
    new SqlConnection(Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")));
```

The injected value is an **ADO.NET connection string** (SQL Server format):

```
Server=sqlserver,1433;Database=master;User Id=sa;Password=...;TrustServerCertificate=True
```

The hostname `sqlserver` is the container's name on the shared Docker network — endpoints reach it by that name without any extra configuration.

## Defaults

| Setting | Default |
|---|---|
| Key | `sqlserver` |
| Environment variable | `SQLSERVER_CONNECTION_STRING` (derived from key) |
| Network alias | `sqlserver` (same as key) |
| Docker image | `mcr.microsoft.com/mssql/server:latest` |

## Customization

```csharp
.UseSqlServer(opts =>
{
    opts.Key = "sqlserver-2";               // changes key; also auto-derives new env var name
    opts.NetworkAlias = "sqlserver-2";      // Docker hostname within the shared network (defaults to key)
    opts.ConnectionStringEnvVarName = "MY_CUSTOM_VAR"; // explicit env var name override
    opts.ImageName = "mcr.microsoft.com/mssql/server:2022-latest";
})
```

For advanced container configuration beyond what `containerOptions` exposes (custom volumes, extra
environment variables, non-standard wait strategies), pass a `containerBuilder` callback:

```csharp
.UseSqlServer(
    containerBuilder: b => b
        .WithLabel("env", "test"))
```

Because Testcontainers builders are immutable, the callback must return the result of the chain.

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
