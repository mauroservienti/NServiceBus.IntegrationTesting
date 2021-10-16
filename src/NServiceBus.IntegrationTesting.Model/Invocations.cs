using System;

namespace NServiceBus.IntegrationTesting
{
    public abstract class Invocation
    {
        public string EndpointName { get; set; }
        public object Message { get; set; }
        public Type MessageType { get; set; }
        public Exception HandlingError { get; set; }
    }

    public class HandlerInvocation : Invocation
    {
        public Type HandlerType { get; set; }
    }

    public class SagaInvocation : Invocation
    {
        public Type SagaType { get; set; }
        public bool NotFound { get; set; }
        public bool IsNew { get; set; }
        public bool IsCompleted { get; set; }
        public object SagaData { get; set; }
    }
}
