using NServiceBus.Pipeline;
using System;
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
            OutgoingMessageOperation outgoingOperation;
            if (context.Headers.ContainsKey(Headers.IsSagaTimeoutMessage))
            {
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

            IntegrationContext.CurrentContext.AddOutogingOperation(outgoingOperation);
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
