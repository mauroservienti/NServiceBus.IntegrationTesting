using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace ScenarioSnippets
{
    public class When_sending_AMessage
    {
        // begin-snippet: scenario-skeleton
        [Test]
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithEndpoint<MyServiceEndpoint>(/*...*/)
                .WithEndpoint<MyOtherServiceEndpoint>(/*...*/)
                .Done(ctx=>false)
                .Run();
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
        class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }

        // end-snippet
    }
}