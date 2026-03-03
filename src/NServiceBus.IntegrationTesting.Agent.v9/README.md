# NServiceBus.IntegrationTesting.AgentV9

The NServiceBus 9 agent for [NServiceBus.IntegrationTesting](https://github.com/mauroservienti/NServiceBus.IntegrationTesting).

Add this package to the `*.Testing` companion project that wraps a **NServiceBus 9** endpoint.
Do **not** add it to the production endpoint project or the test project.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

## Version alignment

| This package | NServiceBus | Target framework |
|---|---|---|
| `NServiceBus.IntegrationTesting.AgentV10` | 10.x | net10.0 |
| `NServiceBus.IntegrationTesting.AgentV9` | 9.x | net8.0 |
| `NServiceBus.IntegrationTesting.AgentV8` | 8.x | net6.0 |

## Usage

**`MyEndpoint.Testing/MyEndpoint.Testing.csproj`** — reference this package:

```xml
<PackageReference Include="NServiceBus.IntegrationTesting.AgentV9" Version="..." />
```

**`MyEndpoint.Testing/Program.cs`** — use `IntegrationTestingBootstrap` as the entry point:

```csharp
await IntegrationTestingBootstrap.RunAsync(
    "MyEndpoint",
    MyEndpointConfig.Create,
    scenarios: [new MyScenario()],
    timeoutRules: [/* optional: shorten saga timeout durations for tests */],
    skipRules: [/* optional: skip specific message types handling during tests */]);
```

`IntegrationTestingBootstrap.RunAsync` wires the agent into the endpoint pipeline, starts the endpoint, connects to the test host via the `NSBUS_TESTING_HOST` environment variable, and blocks until the container is stopped (Ctrl+C or SIGTERM).

## Production / testing separation

The production endpoint (`MyEndpoint/`) has zero dependencies on this package. The `*.Testing` companion project:

1. Calls the same `Config.Create()` factory used in production
2. Registers scenarios — named entry points that run inside the endpoint process using
   the real `IMessageSession`
3. Provides the Dockerfile that builds the testable container image

```text
MyEndpoint/                  ← production code, zero test dependencies
MyEndpoint.Testing/          ← references this package; wraps production config
  Program.cs                 ← IntegrationTestingBootstrap.RunAsync(...)
  MyScenario.cs              ← implements Scenario base class
  Dockerfile                 ← builds the testable container image
MyEndpoint.Tests/            ← references MyEndpoint.Testing with
                               ReferenceOutputAssembly="false"
```

A minimal `Dockerfile` (build context: `src/`) looks like:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MyMessages/MyMessages.csproj MyMessages/
COPY MyEndpoint/MyEndpoint.csproj MyEndpoint/
COPY MyEndpoint.Testing/MyEndpoint.Testing.csproj MyEndpoint.Testing/
RUN dotnet restore MyEndpoint.Testing/MyEndpoint.Testing.csproj

COPY MyMessages/ MyMessages/
COPY MyEndpoint/ MyEndpoint/
COPY MyEndpoint.Testing/ MyEndpoint.Testing/
RUN dotnet publish MyEndpoint.Testing/MyEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyEndpoint.Testing.dll"]
```

`TestEnvironmentBuilder` sets `NSBUS_TESTING_HOST` automatically when it starts the container — endpoints do not configure this.

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
