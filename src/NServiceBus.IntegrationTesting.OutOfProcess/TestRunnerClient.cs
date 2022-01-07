using Grpc.Core;
using System.Text.Json;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class TestRunnerClient
    {
        private Channel channel;
        private TestRunner.TestRunnerClient client;

        public TestRunnerClient(string endpointName)
        {
            //TODO: replace with configurable port
            channel = new Channel("127.0.0.1:30052", ChannelCredentials.Insecure);
            client = new TestRunner.TestRunnerClient(channel);
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
            _ = await client.RecordOutgoingMessageOperationAsync(new OutgoingMessageOperationEvent()
            {
                JsonOperation = JsonSerializer.Serialize(outgoingMessageOperation),
                JsonOperationType = outgoingMessageOperation.GetType().AssemblyQualifiedName
            });
        }
    }
}
