using System.Threading.Tasks;
using MyService;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;

namespace DoneSnippets
{
    public class When_sending_AMessage
    {
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            await Scenario.Define<IntegrationScenarioContext>()
                .WithEndpoint<MyServiceEndpoint>()
                .WithEndpoint<MyOtherServiceEndpoint>()
                // begin-snippet: simple-done-condition
                .Done(ctx =>
                {
                    return ctx.HasFailedMessages();
                })
                // end-snippet
                .Run();

            await Scenario.Define<IntegrationScenarioContext>()
                .WithEndpoint<MyServiceEndpoint>()
                .WithEndpoint<MyOtherServiceEndpoint>()
                // begin-snippet: complete-done-condition
                .Done(ctx =>
                {
                    return ctx.SagaWasInvoked<ASaga>() || ctx.HasFailedMessages();
                })
                // end-snippet
                .Run();
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
        class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
    }
}