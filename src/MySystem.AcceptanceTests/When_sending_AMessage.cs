using MyMessages.Messages;
using MyOtherService;
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
            var theExpectedSagaId = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationContext>()
                .WithEndpoint<MyServiceEndpoint>(g => g.When(b => b.Send(new AMessage() { ThisWillBeTheSagaId = theExpectedSagaId })))
                .WithEndpoint<MyOtherServiceEndpoint>()
                .Done(c =>
                {
                    return
                    (
                        c.HandlerWasInvoked<AMessageHandler>()
                        && c.HandlerWasInvoked<AReplyMessageHandler>()
                        && c.SagaWasInvoked<ASaga>()
                    )
                    || c.HasFailedMessages();
                })
                .Run();

            var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));


            Assert.True(invokedSaga.IsNew);
            Assert.AreEqual("MyService", invokedSaga.EndpointName);
            Assert.True(((ASagaData)invokedSaga.SagaData).SomeId == theExpectedSagaId);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyServiceConfiguration>>();
            }
        }

        class MyOtherServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherServiceEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyOtherServiceConfiguration>>();
            }
        }
    }
}
