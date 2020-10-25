using System;
using System.Threading.Tasks;
using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using NServiceBus.IntegrationTesting;

namespace TimeoutsRescheduleSnippets
{
    public class TimeoutsRescheduleSnippets
    {
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            // begin-snippet: timeouts-reschedule
            var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
                {
                    ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>((msg, delay) =>
                    {
                        return new DoNotDeliverBefore(DateTime.UtcNow.AddSeconds(5));
                    });
                })
                .WithEndpoint<MyServiceEndpoint>(builder => builder.When(session => session.Send("MyService", new StartASaga() { AnIdentifier = Guid.NewGuid() })))
                .Done(ctx => ctx.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || ctx.HasFailedMessages())
                .Run();
            // end-snippet
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder{}
    }
}