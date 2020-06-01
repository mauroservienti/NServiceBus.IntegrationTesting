using NServiceBus.Pipeline;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptSendOperations : Behavior<IOutgoingSendContext>
    {
        readonly string endpointName;

        public InterceptSendOperations(string endpointName)
        {
            this.endpointName = endpointName;
        }

        public override async Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            var sendOperation = new SendOperation
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
