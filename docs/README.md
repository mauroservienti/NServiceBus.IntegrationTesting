# NServiceBus.IntegrationTesting — Documentation

## Guides

| Document | Description |
|---|---|
| [Getting Started](getting-started.md) | End-to-end walkthrough: project structure, scenarios, Dockerfiles, test fixtures, the `ObserveContext` API, and the full API reference. Start here. |
| [Infrastructure Extensibility](infrastructure-extensibility.md) | How to plug in additional infrastructure containers (e.g., Redis, LocalStack) using `UseInfrastructure()` or by authoring a reusable extension package. |
| [Customizing Environment Variable Names](env-var-customization.md) | How to override the default environment variable names (`RABBITMQ_CONNECTION_STRING`, `POSTGRESQL_CONNECTION_STRING`, etc.) when your endpoint already reads connection strings from different variable names. |
| [Test Isolation](test-isolation.md) | Strategies for resetting state between tests when sharing a long-lived `TestEnvironment`: restart endpoint containers or truncate database tables. Examples for NUnit, xUnit, and MSTest. |

## Legacy versions

[V1 and V2 are legacy versions](legacy-v1-and-v2.md) of the testing framework.
