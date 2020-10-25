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
    public class Reschedule_Timeout_Behavior
    {
        [Test]
        public async Task Should_Reschedule_Timeouts()
        {
            var expectedDeliveryAt = new DateTime(2020, 1, 1);

            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.RegisterTimeoutRescheduleRule<AMessage>((msg, currentDelay) =>
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

            var sut = new RescheduleTimeoutsBehavior(scenarioContext); ;
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var rescheduledDoNotDeliverBefore = constraints.OfType<DoNotDeliverBefore>().Single();
            Assert.AreEqual(expectedDeliveryAt, rescheduledDoNotDeliverBefore.At);
        }
    }
}
