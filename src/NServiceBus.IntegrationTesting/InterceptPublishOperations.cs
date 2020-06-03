using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptPublishOperations : Behavior<IOutgoingPublishContext>
    {
        readonly string endpointName;
        readonly IntegrationScenarioContext integrationContext;

        public InterceptPublishOperations(string endpointName, IntegrationScenarioContext integrationContext)
        {
            this.endpointName = endpointName;
            this.integrationContext = integrationContext;
        }

        public override async Task Invoke(IOutgoingPublishContext context, Func<Task> next)
        {
            var sendOperation = new PublishOperation
            {
                SenderEndpoint = endpointName,
                MessageId = context.MessageId,
                MessageType = context.Message.MessageType,
                MessageInstance = context.Message.Instance,
                MessageHeaders = context.Headers
            };

            integrationContext.AddOutogingOperation(sendOperation);
            try 
            {
                await next();
            }
            catch(Exception sendError)
            {
                sendOperation.OperationError = sendError;
                throw;
            }
        }
    }
}
