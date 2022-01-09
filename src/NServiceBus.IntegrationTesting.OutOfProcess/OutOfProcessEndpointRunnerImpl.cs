using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.Grpc.OutOfProcessEndpointRunner;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class OutOfProcessEndpointRunnerImpl : OutOfProcessEndpointRunnerBase
    {
        readonly Action<EndpointStartedEvent> onEndpointStarted;
        readonly Action<RemoteSendMessageOperation> onRemoteSendMessageOperation;
        private readonly Action<(string EndpointName, string PropertyName, string PropertyValue)> onSetContextProperty;

        public OutOfProcessEndpointRunnerImpl(
            Action<EndpointStartedEvent> onEndpointStarted, 
            Action<RemoteSendMessageOperation> onRemoteSendMessageOperation,
            Action<(string EndpointName, string PropertyName, string PropertyValue)> onSetContextProperty
        )
        {
            this.onEndpointStarted = onEndpointStarted;
            this.onRemoteSendMessageOperation = onRemoteSendMessageOperation;
            this.onSetContextProperty = onSetContextProperty;
        }

        public override Task<Empty> OnEndpointStarted(EndpointStartedEvent request, ServerCallContext context)
        {
            onEndpointStarted(request);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RecordSendMessageOperation(RemoteSendMessageOperation request, ServerCallContext context)
        {
            onRemoteSendMessageOperation(request);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SetContextProperty(SetContextPropertyRequest request, ServerCallContext context)
        {
            onSetContextProperty((request.EndpointName, request.PropertyName, request.PropertyValue));

            return Task.FromResult(new Empty());
        }
    }
}
