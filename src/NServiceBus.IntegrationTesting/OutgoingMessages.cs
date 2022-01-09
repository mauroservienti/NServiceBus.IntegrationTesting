using System;
using System.Collections.Generic;

namespace NServiceBus.IntegrationTesting
{
    public abstract class OutgoingMessageOperation
    {
        public string SenderEndpoint { get; set; }
        public string MessageId { get; set; }
        public Type MessageType { get; set; }
        public object MessageInstance { get; set; }
        public Dictionary<string, string> MessageHeaders { get; set; }
        public Exception OperationError { get; set; }
    }

    public class SendOperation : OutgoingMessageOperation { }

    public class ReplyOperation : OutgoingMessageOperation { }

    public class PublishOperation : OutgoingMessageOperation { }

    public class RequestTimeoutOperation : SendOperation 
    {
        public string SagaId { get; set; }
        public string SagaTypeAssemblyQualifiedName { get; set; }
    }
}
