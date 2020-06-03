using NServiceBus.DelayedDelivery;
using NServiceBus.DeliveryConstraints;
using NServiceBus.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class RescheduleTimeoutsBehavior : Behavior<IOutgoingSendContext>
    {
        readonly IntegrationScenarioContext integrationContext;

        public RescheduleTimeoutsBehavior(IntegrationScenarioContext integrationContext)
        {
            this.integrationContext = integrationContext;
        }

        public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            if (integrationContext.TryGetTimeoutRescheduleRule(context.Message.MessageType, out Func<DoNotDeliverBefore, DoNotDeliverBefore> rule))
            {
                var constraints = context.Extensions.Get<List<DeliveryConstraint>>();
                var doNotDeliverBefore = constraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

                var newDoNotDeliverBefore = rule(doNotDeliverBefore);
                constraints.Remove(doNotDeliverBefore);
                constraints.Add(newDoNotDeliverBefore);
            }

            return next();
        }
    }
}
