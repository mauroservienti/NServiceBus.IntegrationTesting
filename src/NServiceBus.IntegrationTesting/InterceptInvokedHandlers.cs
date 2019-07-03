﻿using NServiceBus.Pipeline;
using NServiceBus.Sagas;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class InterceptInvokedHandlers : Behavior<IInvokeHandlerContext>
    {
        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            Invocation invocation = null;
            try
            {
                await next();

                if (context.Extensions.TryGet(out ActiveSagaInstance saga))
                {
                    invocation = IntegrationContext.CurrentContext.CaptureInvokedSaga(new SagaInvocation()
                    {
                        MessageType = context.MessageMetadata.MessageType,
                        NotFound = saga.NotFound,
                        SagaType = saga.NotFound ? null : saga.Instance.GetType(),
                        IsNew = saga.IsNew
                    });
                }
                else
                {
                    invocation = IntegrationContext.CurrentContext.CaptureInvokedHandler(new HandlerInvocation()
                    {
                        MessageType = context.MessageMetadata.MessageType,
                        HandlerType = context.MessageHandler.HandlerType
                    });
                }
            }
            catch (Exception handlingError)
            {
                invocation.HandlingError = handlingError;
                throw;
            }
        }
    }
}