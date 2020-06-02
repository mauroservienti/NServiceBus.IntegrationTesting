using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_requesting_a_timeout
    {
        [Test]
        public async Task It_should_be_rescheduled_and_handled()
        {
            var theExpectedSagaId = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>(ctx=> 
            {
                ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>(currentDelay => 
                {
                    return new DoNotDeliverBefore(DateTime.Now.AddSeconds(5));
                });
            })
            .WithEndpoint<MyServiceEndpoint>(g =>
            {
                g.When(session => session.Send("MyService", new StartASaga() { SomeId = theExpectedSagaId }));
            })
            .Done(c =>
            {
                return
                (
                    c.SagaHandledMessage<ASaga, ASaga.MyTimeout>()
                )
                || c.HasFailedMessages();
            })
            .Run();

            var invokedSagas = context.InvokedSagas.Where(s => s.SagaType == typeof(ASaga));
            var newSaga = invokedSagas.SingleOrDefault(s => s.IsNew);
            var completedSaga = invokedSagas.SingleOrDefault(s => s.IsCompleted);

            Assert.IsNotNull(newSaga);
            Assert.IsNotNull(completedSaga);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<EndpointTemplate<MyServiceConfiguration>>();
            }
        }
    }
}
