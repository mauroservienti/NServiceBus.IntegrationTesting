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
        //TODO: renable when a compatible combination of alphas is available
        //[OneTimeSetUp]
        //public async Task Setup()
        //{
        //    await DockerCompose.Up();
        //}

        //[OneTimeTearDown]
        //public void Teardown()
        //{
        //    DockerCompose.Down();
        //}

        [Test]
        public async Task ASaga_is_completed()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithOutOfProcessEndpoint("MyService", EndpointLifetime.FromSolutionProject("MyService.TestProxy"), behavior =>
                {
                    behavior.When(session =>
                    {
                        return session.Send("MyService", new StartASaga() {AnIdentifier = theExpectedIdentifier});
                    });
                    behavior.When(condition: ctx =>
                    {
                        return ctx.SagaWasInvoked<ASaga>() && ctx.InvokedSagas.Any(s=> s.SagaType == typeof(ASaga) && s.IsNew);
                    },
                    action: session =>
                    {
                        return session.Send("MyService", new CompleteASaga {AnIdentifier = theExpectedIdentifier});
                    });
                })
                .Done(ctx =>
                {
                    return ctx.HasFailedMessages() || ctx.InvokedSagas.Any(s => s.SagaType == typeof(ASaga) && s.IsCompleted);
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
    }
}
