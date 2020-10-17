using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NServiceBus.IntegrationTesting
{
    public class IntegrationScenarioContext : ScenarioContext
    {
        readonly ConcurrentBag<HandlerInvocation> invokedHandlers = new ConcurrentBag<HandlerInvocation>();
        readonly ConcurrentBag<SagaInvocation> invokedSagas = new ConcurrentBag<SagaInvocation>();
        readonly ConcurrentBag<OutgoingMessageOperation> outgoingMessageOperations = new ConcurrentBag<OutgoingMessageOperation>();
        readonly Dictionary<Type, Func<object, DoNotDeliverBefore, DoNotDeliverBefore>> timeoutRescheduleRules = new Dictionary<Type, Func<object, DoNotDeliverBefore, DoNotDeliverBefore>>();

        public IEnumerable<HandlerInvocation> InvokedHandlers { get { return invokedHandlers; } }
        public IEnumerable<SagaInvocation> InvokedSagas { get { return invokedSagas; } }
        public IEnumerable<OutgoingMessageOperation> OutgoingMessageOperations { get { return outgoingMessageOperations; } }

        internal HandlerInvocation CaptureInvokedHandler(HandlerInvocation invocation)
        {
            invokedHandlers.Add(invocation);

            return invocation;
        }

        public void RegisterTimeoutRescheduleRule<TTimeout>(Func<object, DoNotDeliverBefore, DoNotDeliverBefore> rule)
        {
            if (timeoutRescheduleRules.ContainsKey(typeof(TTimeout)))
            {
                throw new NotSupportedException("Only one rule per timeout message type is allowed.");
            }

            timeoutRescheduleRules.Add(typeof(TTimeout), rule);
        }

        internal bool TryGetTimeoutRescheduleRule(Type timeoutMessageType, out Func<object, DoNotDeliverBefore, DoNotDeliverBefore> rule)
        {
            return timeoutRescheduleRules.TryGetValue(timeoutMessageType, out rule);
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

        public bool HandlerWasInvoked<THandler>()
        {
            return InvokedHandlers.Any(invocation => invocation.HandlerType == typeof(THandler));
        }

        public bool SagaWasInvoked<TSaga>() where TSaga : Saga
        {
            return InvokedSagas.Any(invocation => invocation.SagaType == typeof(TSaga));
        }

        public bool SagaWasCompleted<TSaga>() where TSaga : Saga
        {
            return InvokedSagas.Any(invocation => invocation.SagaType == typeof(TSaga) && invocation.IsCompleted);
        }

        public bool MessageWasProcessed<TMessage>()
        {
            return invokedHandlers.Any(invocation => invocation.MessageType == typeof(TMessage))
                || invokedSagas.Any(invocation => invocation.MessageType == typeof(TMessage));
        }

        public bool MessageWasProcessedBySaga<TMessage, TSaga>()
        {
            return invokedSagas.Any(i => i.SagaType == typeof(TSaga) && i.MessageType == typeof(TMessage));
        }

        public bool MessageWasProcessedByHandler<TMessage, THandler>()
        {
            return invokedHandlers.Any(i => i.HandlerType == typeof(THandler) && typeof(TMessage).IsAssignableFrom(i.MessageType));
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
