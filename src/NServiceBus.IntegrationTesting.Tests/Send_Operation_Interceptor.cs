using MyMessages.Messages;
using NServiceBus.Pipeline;
using NServiceBus.Testing;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class Send_Operation_Interceptor
    {
        [Test]
        public async Task Should_Capture_Sent_Message_Operation()
        {
            var integrationContext = new IntegrationScenarioContext();
            
            var sut = new InterceptSendOperations("fake-endpoint", integrationContext); ;
            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };

            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var sendOperation = integrationContext.OutgoingMessageOperations.SingleOrDefault() as SendOperation;

            Assert.AreEqual(1, integrationContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(sendOperation);
        }

        [Test]
        public async Task Should_Capture_TimeoutRequest_Operation()
        {
            var integrationContext = new IntegrationScenarioContext();

            var sut = new InterceptSendOperations("fake-endpoint", integrationContext); ;
            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };
            context.Headers.Add(Headers.SagaId, "a-saga-id");
            context.Headers.Add(Headers.SagaType, "a-saga-type");
            context.Headers.Add(Headers.IsSagaTimeoutMessage, bool.TrueString);

            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var requestTimeoutOperation = integrationContext.OutgoingMessageOperations.SingleOrDefault() as RequestTimeoutOperation;

            Assert.AreEqual(1, integrationContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(requestTimeoutOperation);
            Assert.AreEqual("a-saga-id", requestTimeoutOperation.SagaId);
            Assert.AreEqual("a-saga-type", requestTimeoutOperation.SagaTypeAssemblyQualifiedName);
        }
    }
}
