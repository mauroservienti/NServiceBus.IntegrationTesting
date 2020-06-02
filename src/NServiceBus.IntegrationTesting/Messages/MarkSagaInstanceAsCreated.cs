using System;

namespace NServiceBus.IntegrationTesting.Messages
{
    class MarkSagaInstanceAsCreated
    {
        public Guid SagaId { get; set; }
        public Type SagaDataType { get; set; }
    }
}
