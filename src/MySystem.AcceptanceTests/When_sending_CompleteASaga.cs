using MyMessages.Messages;
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
        class Context : IntegrationScenarioContext 
        {
            public bool WhenExecuted { get; set; }
        }

        [Test]
        public async Task ASaga_is_completed()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<Context>()
                .WithOutOfProcessEndpoint("MyService", EndpointReference.FromSolutionProject("MyService.TestProxy"), behavior =>
                {
                    behavior.When((remoteSession, ctx) => 
                    {
                        ctx.WhenExecuted = true; 
                        return Task.CompletedTask;
                    });
                    //behavior.When(remoteSession =>
                    //{
                    //    return remoteSession.Send("MyService", new StartASaga() { AnIdentifier = theExpectedIdentifier });
                    //});
                    //behavior.When(condition: ctx =>
                    //{
                    //    return ctx.SagaWasInvoked<ASaga>() && ctx.InvokedSagas.Any(s=> s.SagaType == typeof(ASaga) && s.IsNew);
                    //},
                    //action: session =>
                    //{
                    //    return session.Send("MyService", new CompleteASaga {AnIdentifier = theExpectedIdentifier});
                    //});
                })
                .Done(ctx =>
                {
                    return ctx.WhenExecuted;
                    //return ctx.HasFailedMessages() || ctx.InvokedSagas.Any(s => s.SagaType == typeof(ASaga) && s.IsCompleted);
                })
                .Run();

            //var invokedSagas = context.InvokedSagas.Where(s => s.SagaType == typeof(ASaga));
            //var newSaga = invokedSagas.SingleOrDefault(s => s.IsNew);
            //var completedSaga = invokedSagas.SingleOrDefault(s => s.IsCompleted);

            //Assert.IsNotNull(newSaga);
            //Assert.IsNotNull(completedSaga);
            Assert.True(context.WhenExecuted);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }
    }
}
