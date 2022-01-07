using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.TestRunner;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class TestRunnerImpl : TestRunnerBase
    {
        readonly Action<EndpointStartedEvent> onEndpointStarted;
        readonly Action<OutgoingMessageOperation> onOutgoingMessageOperation;

        public TestRunnerImpl(Action<EndpointStartedEvent> onEndpointStarted, Action<OutgoingMessageOperation> onOutgoingMessageOperation)
        {
            this.onEndpointStarted = onEndpointStarted;
            this.onOutgoingMessageOperation = onOutgoingMessageOperation;
        }

        public override Task<Empty> OnEndpointStarted(EndpointStartedEvent request, ServerCallContext context)
        {
            onEndpointStarted(request);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RecordOutgoingMessageOperation(OutgoingMessageOperationEvent request, ServerCallContext context)
        {
            var operationType = System.Type.GetType(request.JsonOperationType);
            var operation = (OutgoingMessageOperation)JsonSerializer.Deserialize(request.JsonOperation, operationType);

            onOutgoingMessageOperation(operation);

            return Task.FromResult(new Empty());
        }
    }
}
