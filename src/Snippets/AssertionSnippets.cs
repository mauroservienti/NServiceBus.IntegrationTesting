using System;
using System.Linq;
using System.Threading.Tasks;
using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace AssertionsSnippets
{
    public class Snippets
    {
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

            // begin-snippet: assert-on-tests-results
            var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

            Assert.True(invokedSaga.IsNew);
            Assert.AreEqual("MyService", invokedSaga.EndpointName);
            Assert.True(((ASagaData)invokedSaga.SagaData).AnIdentifier == theExpectedIdentifier);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
            // end-snippet
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
        class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
    }
}