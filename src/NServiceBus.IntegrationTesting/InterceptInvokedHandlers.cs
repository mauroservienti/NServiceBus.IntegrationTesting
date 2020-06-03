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
            Invocation invocation = null;
            try
            {
                await next();

                if (context.Extensions.TryGet(out ActiveSagaInstance saga))
                {
                    invocation = integrationContext.CaptureInvokedSaga(new SagaInvocation()
                    {
                        NotFound = saga.NotFound,
                        SagaType = saga.NotFound ? null : saga.Instance.GetType(),
                        IsNew = saga.IsNew,
                        IsCompleted = saga.NotFound ? false : saga.Instance.Completed,
                        SagaData = saga.NotFound ? null : saga.Instance.Entity
                    });
                }
                else
                {
                    invocation = integrationContext.CaptureInvokedHandler(new HandlerInvocation()
                    {
                        HandlerType = context.MessageHandler.HandlerType
                    });
                }

                invocation.EndpointName = endpointName;
                invocation.Message = context.MessageBeingHandled;
                invocation.MessageType = context.MessageMetadata.MessageType;
            }
            catch (Exception handlingError)
            {
                invocation.HandlingError = handlingError;
                throw;
            }
        }
    }
}
