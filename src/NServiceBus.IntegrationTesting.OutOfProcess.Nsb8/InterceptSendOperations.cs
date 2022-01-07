using NServiceBus.Pipeline;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class InterceptSendOperations : Behavior<IOutgoingSendContext>
    {
        readonly string endpointName;
        readonly TestRunnerClient testRunnerClient;

        public InterceptSendOperations(string endpointName, TestRunnerClient testRunnerClient)
        {
            this.endpointName = endpointName;
            this.testRunnerClient = testRunnerClient;
        }

        public override async Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            OutgoingMessageOperation outgoingOperation;
            if (context.Headers.ContainsKey(Headers.IsSagaTimeoutMessage) && context.Headers[Headers.IsSagaTimeoutMessage] == bool.TrueString)
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

            await testRunnerClient.RecordOutgoingMessageOperation(outgoingOperation);

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
