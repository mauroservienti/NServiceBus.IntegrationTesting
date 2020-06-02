using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_sending_CompleteASaga
    {
        [Test]
        public async Task ASaga_is_completed()
        {
            var theExpectedSagaId = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationContext>()
                .WithEndpoint<MyServiceEndpoint>(g =>
                {
                    g.When(session => session.Send("MyService", new StartASaga() { SomeId = theExpectedSagaId }));
                    g.When(condition: ctx =>
                    {
                        return ctx.SagaWasInvoked<ASaga>() && ctx.InvokedSagas.Any(s=> s.SagaType == typeof(ASaga) && s.IsNew);
                    }, 
                    action: session => 
                    {
                        return session.Send("MyService", new CompleteASaga { SomeId = theExpectedSagaId }); ; 
                    });
                })
                .Done(c =>
                {
                    return
                    (
                        c.SagaWasInvoked<ASaga>()
                        && c.InvokedSagas.Any(s => s.SagaType == typeof(ASaga) && s.IsCompleted)
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
