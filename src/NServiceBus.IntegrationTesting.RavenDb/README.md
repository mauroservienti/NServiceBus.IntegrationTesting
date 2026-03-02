# NServiceBus.IntegrationTesting.RavenDb

Adds a RavenDB container to [NServiceBus.IntegrationTesting](https://github.com/mauroservienti/NServiceBus.IntegrationTesting) test environments.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop/) installed and running

## Usage

Add this package to your test project alongside `NServiceBus.IntegrationTesting`, then call `.UseRavenDB()` on `TestEnvironmentBuilder`:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .UseRavenDB()
    .AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile")
    .StartAsync();
```

The RavenDB container is joined to the shared Docker network and `TestEnvironmentBuilder` injects the `RAVENDB_URL` environment variable into every endpoint container. No Docker network wiring is needed on your side — your endpoint just reads the variable at startup:

```csharp
var persistence = endpointConfiguration.UsePersistence<RavenDBPersistence>();
persistence.SetDefaultDocumentStore(new DocumentStore
{
    Urls = [Environment.GetEnvironmentVariable("RAVENDB_URL")!],
    Database = "MyEndpoint"
});
```

The container runs with `--Setup.Mode=None` so no setup wizard is required. The injected value is an **HTTP URL**:

```
http://ravendb:8080
```

## Defaults

| Setting | Default |
|---|---|
| Environment variable | `RAVENDB_URL` |
| Docker image | `ravendb/ravendb:latest` |
| Port | `8080` |

## Customization

```csharp
.UseRavenDB(opts =>
{
    opts.ConnectionStringEnvVarName = "MY_CUSTOM_VAR";
    opts.ImageName = "ravendb/ravendb:6.0-latest";
    opts.Port = 8080;
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

For the full walkthrough and API reference, see the **[documentation](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/docs/README.md)**.
