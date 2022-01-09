using Grpc.Core;
using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using System;
using System.Text.Json;
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

        public async Task RecordSendMessageOperation(RemoteSendMessageOperation request)
        {
            _ = await client.RecordSendMessageOperationAsync(request);
        }

        public async Task RecordRequestTimeoutOperation(RemoteRequestTimeoutOperation request)
        {
            _ = await client.RecordRequestTimeoutOperationAsync(request);
        }

        public async Task RecordInvokedHandler(RemoteHandlerInvocation request)
        {
            _ = await client.RecordInvokedHandlerAsync(request);
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
