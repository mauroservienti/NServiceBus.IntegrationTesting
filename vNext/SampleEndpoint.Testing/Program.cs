using NServiceBus.IntegrationTesting.Agent;
using SampleEndpoint;
using SampleEndpoint.Testing;

await IntegrationTestingBootstrap.RunAsync(
    "SampleEndpoint",
    SampleEndpointConfig.Create,
    [new SomeMessageScenario()]);
