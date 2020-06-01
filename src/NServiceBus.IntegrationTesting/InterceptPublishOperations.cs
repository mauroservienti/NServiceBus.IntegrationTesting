using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptPublishOperations : Behavior<IOutgoingPublishContext>
    {
        readonly string endpointName;

        public InterceptPublishOperations(string endpointName)
        {
            this.endpointName = endpointName;
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

            IntegrationContext.CurrentContext.AddOutogingOperation(sendOperation);
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
