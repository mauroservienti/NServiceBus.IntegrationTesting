using System.Threading.Tasks;
using MyMessages.Messages;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;

namespace KickOffSnippets
{
    public class When_sending_AMessage
    {
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            // begin-snippet: kick-off-choreography
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithEndpoint<MyServiceEndpoint>(builder => builder.When(session => session.Send(new AMessage())))
            // end-snippet
                .WithEndpoint<MyOtherServiceEndpoint>()
                .Done(ctx =>false)
                .Run();
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
        class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
    }
}