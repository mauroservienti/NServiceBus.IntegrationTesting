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
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithGenericHostEndpoint("MyService", configPreview => Program.CreateHostBuilder(new string[0], configPreview).Build(), behavior =>
                {
                    behavior.When(session =>
                    {
                        return session.SendLocal(new StartASaga() {AnIdentifier = theExpectedIdentifier});
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
