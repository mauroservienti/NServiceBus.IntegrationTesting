//using NServiceBus.IntegrationTesting.OutOfProcess;
//using NServiceBus.Pipeline;
//using NServiceBus.Sagas;
//using System;
//using System.Threading.Tasks;

//namespace NServiceBus.IntegrationTesting
//{
//    class InterceptInvokedHandlers : Behavior<IInvokeHandlerContext>
//    {
//        readonly string endpointName;
//        readonly OutOfProcessEndpointRunnerClient testRunnerClient;

//        public InterceptInvokedHandlers(string endpointName, OutOfProcessEndpointRunnerClient testRunnerClient)
//        {
//            this.endpointName = endpointName;
//            this.testRunnerClient = testRunnerClient;
//        }

//        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
//        {
//            try
//            {
//                await next();
//                CaptureInvocation(context, null);
//            }
//            catch (Exception ex)
//            {
//                CaptureInvocation(context, ex);
//                throw;
//            }
//        }

//        void CaptureInvocation(IInvokeHandlerContext context, Exception handlingError)
//        {
//            //var request = new InvokedHandlerEvent()
//            //{
//            //    EndpointName = handlerInvocation.EndpointName,
//            //    HandlerTypeAssemblyQualifiedName = handlerInvocation.HandlerType.AssemblyQualifiedName,
//            //    MessageInstanceJson = JsonSerializer.Serialize(handlerInvocation.Message),
//            //    MessageTypeAssemblyQualifiedName = handlerInvocation.MessageType.AssemblyQualifiedName,
//            //    HandlingErrorJson = handlerInvocation.HandlingError != null
//            //        ? JsonSerializer.Serialize(handlerInvocation.HandlingError)
//            //        : string.Empty,
//            //    HandlingErrorTypeAssemblyQualifiedName = handlerInvocation.HandlingError != null
//            //        ? handlerInvocation.HandlingError.GetType().AssemblyQualifiedName
//            //        : string.Empty,
//            //};

//            try
//            {
//                if (context.Extensions.TryGet(out ActiveSagaInstance saga))
//                {
//                    //var sagaInvocation = saga.NotFound
//                    //    ? new SagaInvocation()
//                    //    {
//                    //        NotFound = true
//                    //    }
//                    //    : new SagaInvocation()
//                    //    {
//                    //        NotFound = false,
//                    //        SagaType = saga.Instance.GetType(),
//                    //        IsNew = saga.IsNew,
//                    //        IsCompleted = saga.Instance.Completed,
//                    //        SagaData = saga.Instance.Entity
//                    //    };

//                    //FillCommonDetails(context, handlingError, sagaInvocation);

//                    //await testRunnerClient.RecordInvokedSaga(sagaInvocation);
//                }
//                else
//                {
//                    var handlerInvocation = new HandlerInvocation() { HandlerType = context.MessageHandler.HandlerType };
//                    FillCommonDetails(context, handlingError, handlerInvocation);

//                    await testRunnerClient.RecordInvokedHandler(handlerInvocation);
//                }
//            }
//            catch (Exception e)
//            {
//                throw new InvalidOperationException("Testing infrastructure failure while capturing handler/saga invocation.", e);
//            }
//        }

//        void FillCommonDetails(IInvokeHandlerContext context, Exception handlingError, Invocation invocation)
//        {
//            invocation.EndpointName = endpointName;
//            invocation.Message = context.MessageBeingHandled;
//            invocation.MessageType = context.MessageMetadata.MessageType;
//            invocation.HandlingError = handlingError;
//        }
//    }
//}