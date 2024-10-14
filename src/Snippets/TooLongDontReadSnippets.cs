using System;
using System.Linq;
using System.Threading.Tasks;
using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace TooLongDontReadSnippets
{
    public class Snippets
    {
        // begin-snippet: too-long-dont-read-full-test
        [Test]
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithEndpoint<MyServiceEndpoint>(behavior =>
                {
                    behavior.When(session => session.Send(new AMessage() {AnIdentifier = theExpectedIdentifier}));
                })
                .WithEndpoint<MyOtherServiceEndpoint>()
                .Done(c => c.SagaWasInvoked<ASaga>() || c.HasFailedMessages())
                .Run();

            var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

            Assert.That(invokedSaga.IsNew, Is.True);
            Assert.That("MyService", Is.EqualTo(invokedSaga.EndpointName));
            Assert.That(((ASagaData)invokedSaga.SagaData).AnIdentifier, Is.EqualTo(theExpectedIdentifier));
            Assert.That(context.HasFailedMessages(), Is.False);
            Assert.That(context.HasHandlingErrors(), Is.False);
        }
        // end-snippet

        class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
        class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
    }
}