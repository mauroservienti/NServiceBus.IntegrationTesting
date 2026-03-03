# NServiceBus.IntegrationTesting.RabbitMQ

Adds a RabbitMQ container to [NServiceBus.IntegrationTesting](https://github.com/mauroservienti/NServiceBus.IntegrationTesting) test environments.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop/) installed and running

## Usage

Add this package to your test project alongside `NServiceBus.IntegrationTesting`, then call `.UseRabbitMQ()` on `TestEnvironmentBuilder`:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMQ()
    .AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile")
    .StartAsync();
```

The RabbitMQ container is joined to the shared Docker network and `TestEnvironmentBuilder` injects the `RABBITMQ_CONNECTION_STRING` environment variable into every endpoint container. No Docker network wiring is needed on your side — your endpoint just reads the variable at startup:

```csharp
var transport = new RabbitMQTransport(
    RoutingTopology.Conventional(QueueType.Classic),
    Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")!);
```

The injected value uses the **NServiceBus RabbitMQ transport** connection string format:

```
host=rabbitmq;username=guest;password=guest
```

The hostname `rabbitmq` is the container's name on the shared Docker network — endpoints reach it by that name without any extra configuration.

## Defaults

| Setting | Default |
|---|---|
| Key | `rabbitmq` |
| Environment variable | `RABBITMQ_CONNECTION_STRING` (derived from key) |
| Network alias | `rabbitmq` (same as key) |
| Docker image | `rabbitmq:management` |
| Username | `guest` (Testcontainers default) |
| Password | `guest` (Testcontainers default) |

## Customization

```csharp
.UseRabbitMQ(opts =>
{
    opts.Key = "rabbitmq-2";                // changes key; also auto-derives new env var name
    opts.NetworkAlias = "rabbitmq-2";       // Docker hostname within the shared network (defaults to key)
    opts.ConnectionStringEnvVarName = "MY_CUSTOM_VAR"; // explicit env var name override
    opts.ImageName = "rabbitmq:3-management";
    opts.Username = "myuser";               // injected into both the container and the connection string
    opts.Password = "mypassword";
})
```

For advanced container configuration beyond what `containerOptions` exposes (custom volumes, extra
environment variables, non-standard wait strategies), pass a `containerBuilder` callback:

```csharp
.UseRabbitMQ(
    containerBuilder: b => b
        .WithEnvironment("RABBITMQ_DEFAULT_VHOST", "test")
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
