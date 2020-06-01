using NServiceBus.AcceptanceTesting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NServiceBus.IntegrationTesting
{
    public class IntegrationContext : ScenarioContext
    {
        readonly ConcurrentBag<HandlerInvocation> invokedHandlers = new ConcurrentBag<HandlerInvocation>();
        readonly ConcurrentBag<SagaInvocation> invokedSagas = new ConcurrentBag<SagaInvocation>();
        readonly ConcurrentBag<OutgoingMessageOperation> outgoingMessageOperations = new ConcurrentBag<OutgoingMessageOperation>();

        public IEnumerable<HandlerInvocation> InvokedHandlers { get { return invokedHandlers; } }
        public IEnumerable<SagaInvocation> InvokedSagas { get { return invokedSagas; } }
        public IEnumerable<OutgoingMessageOperation> OutgoingMessageOperations { get { return outgoingMessageOperations; } }


        internal HandlerInvocation CaptureInvokedHandler(HandlerInvocation invocation)
        {
            invokedHandlers.Add(invocation);

            return invocation;
        }

        internal void AddOutogingOperation(OutgoingMessageOperation outgoingMessageOperation)
        {
            outgoingMessageOperations.Add(outgoingMessageOperation);
        }

        internal SagaInvocation CaptureInvokedSaga(SagaInvocation invocation)
        {
            invokedSagas.Add(invocation);

            return invocation;
        }

        static PropertyInfo GetScenarioContextCurrentProperty()
        {
            return typeof(ScenarioContext).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static IntegrationContext CurrentContext
        {
            get
            {
                var pi = GetScenarioContextCurrentProperty();
                var current = (IntegrationContext)pi.GetMethod.Invoke(null, null);

                return current;
            }
        }

        public bool HandlerWasInvoked<THandler>()
        {
            return InvokedHandlers.Any(invocation => invocation.HandlerType == typeof(THandler));
        }

        public bool SagaWasInvoked<TSaga>() where TSaga : Saga
        {
            return InvokedSagas.Any(invocation => invocation.SagaType == typeof(TSaga));
        }

        public bool HasFailedMessages()
        {
            return !FailedMessages.IsEmpty;
        }

        public bool HasHandlingErrors()
        {
            return invokedHandlers.Any(invocation => invocation.HandlingError != null)
                || invokedSagas.Any(invocation => invocation.HandlingError != null);
        }
    }
}
