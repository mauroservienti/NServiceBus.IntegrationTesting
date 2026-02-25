using AnotherEndpoint;
using NServiceBus.IntegrationTesting.Agent;

await IntegrationTestingBootstrap.RunAsync(
    "AnotherEndpoint",
    AnotherEndpointConfig.Create);
