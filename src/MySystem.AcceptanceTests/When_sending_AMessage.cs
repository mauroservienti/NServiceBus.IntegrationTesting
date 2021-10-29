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
    public class When_sending_AMessage
    {
        [Test]
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithGenericHostEndpoint("MyService", configPreview => Program.CreateHostBuilder(new string[0], configPreview).Build(), behavior =>
                {
                    behavior.When(session => session.Send(new AMessage() { AnIdentifier = theExpectedIdentifier }));
                })
                .WithEndpoint<MyOtherServiceEndpoint>()
                .Done(c => c.SagaWasInvoked<ASaga>() || c.HasFailedMessages())
                .Run();

            var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

            Assert.True(invokedSaga.IsNew);
            Assert.AreEqual("MyService", invokedSaga.EndpointName);
            Assert.True(((ASagaData)invokedSaga.SagaData).AnIdentifier == theExpectedIdentifier);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class MyOtherServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherServiceEndpoint()
            {
                EndpointSetup<MyOtherServiceTemplate>();
            }
        }
    }
}
