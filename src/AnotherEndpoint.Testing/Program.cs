using AnotherEndpoint;
using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;

await IntegrationTestingBootstrap.RunAsync(
    "AnotherEndpoint",
    CreateConfig);

static EndpointConfiguration CreateConfig()
{
    var config = AnotherEndpointConfig.Create();
    // No retries so failing messages reach the error queue immediately in tests.
    config.Recoverability().Immediate(s => s.NumberOfRetries(0));
    config.Recoverability().Delayed(s => s.NumberOfRetries(0));
    return config;
}
