using NServiceBus.AcceptanceTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NServiceBus.IntegrationTesting
{
    public abstract class IntegrationContext : ScenarioContext
    {
        List<HandlerInvocation> invokedHandlers = new List<HandlerInvocation>();

        public IEnumerable<HandlerInvocation> InvokedHandlers { get { return invokedHandlers; } }

        internal HandlerInvocation CaptureInvokedHandler(HandlerInvocation invocation)
        {
            invokedHandlers.Add(invocation);

            return invocation;
        }

        List<SagaInvocation> invokedSagas = new List<SagaInvocation>();

        public IEnumerable<SagaInvocation> InvokedSagas { get { return invokedSagas; } }


        internal SagaInvocation CaptureInvokedSaga(SagaInvocation invocation)
        {
            invokedSagas.Add(invocation);

            return invocation;
        }

        static PropertyInfo GetCurrentProperty()
        {
            return typeof(ScenarioContext).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static IntegrationContext CurrentContext
        {
            get
            {
                var pi = GetCurrentProperty();
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
