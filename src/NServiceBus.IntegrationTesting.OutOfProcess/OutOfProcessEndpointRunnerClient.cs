using Grpc.Core;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class OutOfProcessEndpointRunnerClient
    {
        Channel channel;
        OutOfProcessEndpointRunner.OutOfProcessEndpointRunnerClient client;

        public OutOfProcessEndpointRunnerClient(string endpointName, int serverPort)
        {
            channel = new Channel($"127.0.0.1:{serverPort}", ChannelCredentials.Insecure);
            client = new OutOfProcessEndpointRunner.OutOfProcessEndpointRunnerClient(channel);
            EndpointName = endpointName;
        }

        public string EndpointName { get; }

        public async Task OnEndpointStarted()
        {
            _ = await client.OnEndpointStartedAsync(new EndpointStartedEvent()
            {
                EndpointName = EndpointName
            });
        }

        public async Task RecordOutgoingMessageOperation(OutgoingMessageOperation outgoingMessageOperation)
        {
            var request = new OutgoingMessageOperationEvent()
            {
                OperationTypeAssemblyQualifiedName = outgoingMessageOperation.GetType().AssemblyQualifiedName,
                SenderEndpoint = outgoingMessageOperation.SenderEndpoint,
                //MessageInstanceJson = outgoingMessageOperation.MessageInstance != null
                //                ? JsonSerializer.Serialize(outgoingMessageOperation.MessageInstance)
                //                : null,
                MessageId = outgoingMessageOperation.MessageId,
                //MessageHeadersJson = outgoingMessageOperation.MessageHeaders != null
                //                ? JsonSerializer.Serialize(outgoingMessageOperation.MessageHeaders)
                //                : null,
                MessageTypeAssemblyQualifiedName = outgoingMessageOperation.MessageType != null
                                ? outgoingMessageOperation.MessageType.AssemblyQualifiedName
                                : null,
                //OperationErrorTypeAssemblyQualifiedName = outgoingMessageOperation.OperationError != null
                //                ? outgoingMessageOperation.OperationError.GetType().AssemblyQualifiedName
                //                : null,
                //OperationErrorJson = outgoingMessageOperation.OperationError != null
                //                ? JsonSerializer.Serialize(outgoingMessageOperation.OperationError)
                //                : null
            };
            _ = await client.RecordOutgoingMessageOperationAsync(request);
        }

        public async Task SetContextProperty(string propertyName, string propertyValue)
        {
            _ = await client.SetContextPropertyAsync(new SetContextPropertyRequest()
            {
                EndpointName = EndpointName,
                PropertyName = propertyName,
                PropertyValue = propertyValue
            });
        }
    }
}
