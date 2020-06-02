using MyMessages.Messages;
using NServiceBus.DelayedDelivery;
using NServiceBus.DeliveryConstraints;
using NServiceBus.Pipeline;
using NServiceBus.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class Send_Operation_Interceptor
    {
        [Test]
        public async Task Should_Capture_Sent_Message_Operation()
        {
            var scenarioContext = new IntegrationScenarioContext();
            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };

            var sut = new InterceptSendOperations("fake-endpoint", scenarioContext);
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var sendOperation = scenarioContext.OutgoingMessageOperations.SingleOrDefault() as SendOperation;

            Assert.AreEqual(1, scenarioContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(sendOperation);
        }

        [Test]
        public async Task Should_Capture_TimeoutRequest_Operation()
        {
            var expectedSagaId = "a-saga-id";
            var expectedSagaType = "a-saga-type";

            var scenarioContext = new IntegrationScenarioContext();
            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };
            context.Headers.Add(Headers.SagaId, expectedSagaId);
            context.Headers.Add(Headers.SagaType, expectedSagaType);
            context.Headers.Add(Headers.IsSagaTimeoutMessage, bool.TrueString);

            var sut = new InterceptSendOperations("fake-endpoint", scenarioContext); ;
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var requestTimeoutOperation = scenarioContext.OutgoingMessageOperations.SingleOrDefault() as RequestTimeoutOperation;

            Assert.AreEqual(1, scenarioContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(requestTimeoutOperation);
            Assert.AreEqual(expectedSagaId, requestTimeoutOperation.SagaId);
            Assert.AreEqual(expectedSagaType, requestTimeoutOperation.SagaTypeAssemblyQualifiedName);
        }

        [Test]
        public async Task Should_Reschedule_Timeouts()
        {
            var expectedDeliveryAt = new DateTime(2020, 1, 1);

            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.RegisterTimeoutRescheduleRule<AMessage>(currentDelay => 
            {
                return new DoNotDeliverBefore(expectedDeliveryAt);
            });

            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };
            context.Headers.Add(Headers.SagaId, "a-saga-id");
            context.Headers.Add(Headers.SagaType, "a-saga-type");
            context.Headers.Add(Headers.IsSagaTimeoutMessage, bool.TrueString);

            var constraints = new List<DeliveryConstraint>
            {
                new DoNotDeliverBefore(new DateTime(2030, 1, 1))
            };
            context.Extensions.Set(constraints);

            var sut = new InterceptSendOperations("fake-endpoint", scenarioContext); ;
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var rescheduledDoNotDeliverBefore = constraints.OfType<DoNotDeliverBefore>().Single();
            Assert.AreEqual(expectedDeliveryAt, rescheduledDoNotDeliverBefore.At);
        }
    }
}
