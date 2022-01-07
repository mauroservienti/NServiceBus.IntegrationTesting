using Grpc.Core;
using System;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class TestRunnerServer
    {
        private readonly TestRunnerImpl testRunner;
        readonly Server server;

        public TestRunnerServer(Action<EndpointStartedEvent> onEndpointStarted, Action<OutgoingMessageOperation> onOutgoingMessageOperation)
        {
            testRunner = new TestRunnerImpl(onEndpointStarted, onOutgoingMessageOperation);

            server = new Server
            {
                Services = { TestRunner.BindService(testRunner) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", 30052, ServerCredentials.Insecure) }
            };
            server.Start();
        }
    }
}
