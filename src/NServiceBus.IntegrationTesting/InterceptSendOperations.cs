using NServiceBus.DelayedDelivery;
using NServiceBus.DeliveryConstraints;
using NServiceBus.Pipeline;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptSendOperations : Behavior<IOutgoingSendContext>
    {
        readonly string endpointName;
        readonly IntegrationScenarioContext integrationContext;

        public InterceptSendOperations(string endpointName, IntegrationScenarioContext integrationContext)
        {
            this.endpointName = endpointName;
            this.integrationContext = integrationContext;
        }

        public override async Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            OutgoingMessageOperation outgoingOperation;
            if (context.Headers.ContainsKey(Headers.IsSagaTimeoutMessage) && context.Headers[Headers.IsSagaTimeoutMessage] == bool.TrueString)
            {
                if (integrationContext.TryGetTimeoutRescheduleRule(context.Message.MessageType, out Func<DoNotDeliverBefore, DoNotDeliverBefore> rule)) 
                {
                    var constraints = context.Extensions.Get<List<DeliveryConstraint>>();
                    var doNotDeliverBefore = constraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

                    var newDoNotDeliverBefore = rule(doNotDeliverBefore);
                    constraints.Remove(doNotDeliverBefore);
                    constraints.Add(newDoNotDeliverBefore);
                }

                outgoingOperation = new RequestTimeoutOperation()
                {
                    SagaId = context.Headers[Headers.SagaId],
                    SagaTypeAssemblyQualifiedName = context.Headers[Headers.SagaType]
                };
            }
            else
            {
                outgoingOperation = new SendOperation();
            }

            outgoingOperation.SenderEndpoint = endpointName;
            outgoingOperation.MessageId = context.MessageId;
            outgoingOperation.MessageType = context.Message.MessageType;
            outgoingOperation.MessageInstance = context.Message.Instance;
            outgoingOperation.MessageHeaders = context.Headers;

            integrationContext.AddOutogingOperation(outgoingOperation);
            try
            {
                await next();
            }
            catch (Exception sendError)
            {
                outgoingOperation.OperationError = sendError;
                throw;
            }
        }
    }
}
