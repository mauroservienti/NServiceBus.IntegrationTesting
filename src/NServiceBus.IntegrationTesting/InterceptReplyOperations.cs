using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptReplyOperations : Behavior<IOutgoingReplyContext>
    {
        readonly string endpointName;

        public InterceptReplyOperations(string endpointName)
        {
            this.endpointName = endpointName;
        }

        public override async Task Invoke(IOutgoingReplyContext context, Func<Task> next)
        {
            var sendOperation = new ReplyOperation
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
