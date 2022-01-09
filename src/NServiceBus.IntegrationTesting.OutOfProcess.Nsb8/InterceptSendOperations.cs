using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using NServiceBus.Pipeline;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class InterceptSendOperations : Behavior<IOutgoingSendContext>
    {
        readonly string endpointName;
        readonly OutOfProcessEndpointRunnerClient testRunnerClient;

        public InterceptSendOperations(string endpointName, OutOfProcessEndpointRunnerClient testRunnerClient)
        {
            this.endpointName = endpointName;
            this.testRunnerClient = testRunnerClient;
        }

        public override async Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            RemoteRequestTimeoutOperation requestTimeoutOp = null;
            RemoteSendMessageOperation sendOp = null;

            if (context.Headers.ContainsKey(Headers.IsSagaTimeoutMessage) && context.Headers[Headers.IsSagaTimeoutMessage] == bool.TrueString)
            {
                requestTimeoutOp = new RemoteRequestTimeoutOperation()
                {
                    SenderEndpoint = endpointName,
                    MessageInstanceJson = JsonSerializer.Serialize(context.Message.Instance, context.Message.MessageType),
                    MessageId = context.MessageId,
                    MessageTypeAssemblyQualifiedName = context.Message.MessageType.AssemblyQualifiedName,
                    SagaId = context.Headers[Headers.SagaId],
                    SagaTypeAssemblyQualifiedName = context.Headers[Headers.SagaType]
                };
                requestTimeoutOp.MessageHeaders.AddRange(context.Headers.Select(kvp => new Header()
                {
                    Key = kvp.Key,
                    Value = kvp.Value ?? ""
                }));
            }
            else
            {
                sendOp = new RemoteSendMessageOperation()
                {
                    SenderEndpoint = endpointName,
                    MessageInstanceJson = JsonSerializer.Serialize(context.Message.Instance, context.Message.MessageType),
                    MessageId = context.MessageId,
                    MessageTypeAssemblyQualifiedName = context.Message.MessageType.AssemblyQualifiedName
                };
                sendOp.MessageHeaders.AddRange(context.Headers.Select(kvp => new Header()
                {
                    Key = kvp.Key,
                    Value = kvp.Value ?? ""
                }));
            }

            Exception ex = null;

            try
            {
                await next();
            }
            catch (Exception sendError)
            {
                ex = sendError;
                throw;
            }
            finally
            {
                if (context.Headers.ContainsKey(Headers.IsSagaTimeoutMessage) && context.Headers[Headers.IsSagaTimeoutMessage] == bool.TrueString)
                {
                    if (ex != null)
                    {
                        requestTimeoutOp.OperationErrorTypeAssemblyQualifiedName = ex.GetType().AssemblyQualifiedName;
                        requestTimeoutOp.OperationErrorJson = JsonSerializer.Serialize(ex);
                    }
                    await testRunnerClient.RecordRequestTimeoutOperation(requestTimeoutOp);
                }
                else
                {
                    if (ex != null)
                    {
                        sendOp.OperationErrorTypeAssemblyQualifiedName = ex.GetType().AssemblyQualifiedName;
                        sendOp.OperationErrorJson = JsonSerializer.Serialize(ex);
                    }
                    await testRunnerClient.RecordSendMessageOperation(sendOp);
                }
            }
        }
    }
}
