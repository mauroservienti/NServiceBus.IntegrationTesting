﻿using Grpc.Core;
using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class OutOfProcessEndpointRunnerServer
    {
        private readonly OutOfProcessEndpointRunnerImpl testRunner;
        readonly Server server;

        public OutOfProcessEndpointRunnerServer(
            int port,
            Action<EndpointStartedEvent> onEndpointStarted,
            Action<RemoteSendMessageOperation> onRemoteSendMessageOperation, 
            Action<(string EndpointName, string PropertyName, string PropertyValue)> onSetContextProperty)
        {
            testRunner = new OutOfProcessEndpointRunnerImpl(
                onEndpointStarted,
                onRemoteSendMessageOperation, 
                onSetContextProperty);

            server = new Server
            {
                Services = { OutOfProcessEndpointRunner.BindService(testRunner) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            server.Start();
        }

        public Task Stop()
        {
            return server.ShutdownAsync();
        }
    }
}
