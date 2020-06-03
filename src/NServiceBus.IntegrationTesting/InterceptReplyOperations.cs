using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptReplyOperations : Behavior<IOutgoingReplyContext>
    {
        readonly string endpointName;
        readonly IntegrationScenarioContext integrationContext;

        public InterceptReplyOperations(string endpointName, IntegrationScenarioContext integrationContext)
        {
            this.endpointName = endpointName;
            this.integrationContext = integrationContext;
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
