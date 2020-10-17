using NServiceBus.Pipeline;
using NServiceBus.Sagas;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptInvokedHandlers : Behavior<IInvokeHandlerContext>
    {
        readonly string endpointName;
        readonly IntegrationScenarioContext integrationContext;

        public InterceptInvokedHandlers(string endpointName, IntegrationScenarioContext integrationContext)
        {
            this.endpointName = endpointName;
            this.integrationContext = integrationContext;
        }

        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            try
            {
                await next();
                CaptureInvocation(context, null);
            }
            catch (Exception ex)
            {
                CaptureInvocation(context, ex);
                throw;
            }
        }

        void CaptureInvocation(IInvokeHandlerContext context, Exception handlingError)
        {
            try
            {
                Invocation invocation = null;
                if (context.Extensions.TryGet(out ActiveSagaInstance saga))
                {
                    var sagaInvocation = saga.NotFound
                        ? new SagaInvocation()
                        {
                            NotFound = true
                        }
                        : new SagaInvocation()
                        {
                            NotFound = false,
                            SagaType = saga.Instance.GetType(),
                            IsNew = saga.IsNew,
                            IsCompleted = saga.Instance.Completed,
                            SagaData = saga.Instance.Entity
                        };

                    invocation = integrationContext.CaptureInvokedSaga(sagaInvocation);
                }
                else
                {
                    invocation = integrationContext.CaptureInvokedHandler(new HandlerInvocation() {HandlerType = context.MessageHandler.HandlerType});
                }

                invocation.EndpointName = endpointName;
                invocation.Message = context.MessageBeingHandled;
                invocation.MessageType = context.MessageMetadata.MessageType;
                invocation.HandlingError = handlingError;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Testing infrastructure failure while capturing handler/saga invocation.", e);
            }
        }
    }
}