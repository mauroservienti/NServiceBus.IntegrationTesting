using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using YetAnotherEndpoint;
using YetAnotherEndpoint.Testing;

await IntegrationTestingBootstrap.RunAsync(
    "YetAnotherEndpoint",
    CreateConfig,
    scenarios: [new YetAnotherMessageScenario()]);

static EndpointConfiguration CreateConfig()
{
    var config = YetAnotherEndpointConfig.Create();
    config.Recoverability().Immediate(s => s.NumberOfRetries(0));
    config.Recoverability().Delayed(s => s.NumberOfRetries(0));
    return config;
}
