using NServiceBus.DelayedDelivery;
using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;
using NServiceBus.Transport;

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
            if (integrationContext.TryGetTimeoutRescheduleRule(context.Message.MessageType, out Func<object, DoNotDeliverBefore, DoNotDeliverBefore> rule))
            {
                var constraints = context.Extensions.Get<DispatchProperties>();
                var doNotDeliverBefore = constraints.DoNotDeliverBefore;

                var newDoNotDeliverBefore = rule(context.Message, doNotDeliverBefore);
                if(newDoNotDeliverBefore != doNotDeliverBefore)
                {
                   constraints.DoNotDeliverBefore = newDoNotDeliverBefore;
                }
            }

            return next();
        }
    }
}
