using NServiceBus.IntegrationTesting.Agent;
using SampleEndpoint;
using SampleEndpoint.Handlers;
using SampleEndpoint.Testing;

await IntegrationTestingBootstrap.RunAsync(
    "SampleEndpoint",
    SampleEndpointConfig.Create,
    scenarios: [new SomeMessageScenario()],
    timeoutRules: [TimeoutRule.For<SomeReplySagaTimeout>(TimeSpan.FromSeconds(5))]);
