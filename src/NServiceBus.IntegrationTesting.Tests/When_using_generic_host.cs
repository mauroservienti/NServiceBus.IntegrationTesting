using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using System;
using NServiceBus.IntegrationTesting.Tests.TestEndpoint;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class When_using_generic_host
    {
        [Test]
        public void Ensure_endpoint_is_correctly_configured()
        {
            var endpointName = "NServiceBus.IntegrationTesting.Tests.TestEndpoint";
            var expectedMessage = $"Endpoint {endpointName} is not correctly configured to be tested. " +
                $"Make sure to pass the EndpointConfiguration instance to the Action<EndpointConfiguration> " +
                $"provided by WithGenericHostEndpoint tests setup method.";

            var ex = _ = Assert.ThrowsAsync<Exception>(async () =>
            {
                await Scenario.Define<IntegrationScenarioContext>()
                    .WithGenericHostEndpoint(
                        endpointName,
                        configPreview => Program.CreateHostBuilder(new string[0]).Build(),
                        _ => { })
                    .Run();
            });

            Assert.AreEqual(expectedMessage, ex.Message);
        }
    }
}
