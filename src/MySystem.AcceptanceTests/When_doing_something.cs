using MyMessages.Messages;
using MyOtherService;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_doing_something
    {
        [Test]
        public async Task something_should_happen()
        {
            var expectedSomeId = Guid.NewGuid();
            var context = await Scenario.Define<Context>()
                .WithEndpoint<MyServiceEndpoint>(g => g.When(b => b.Send(new AMessage() { ThisWillBeTheSagaId = expectedSomeId })))
                .WithEndpoint<MyOtherEndpointEndpoint>()
                .Done(c =>
                {
                    return
                    (
                        c.HandlerWasInvoked<AMessageHandler>()
                        && c.HandlerWasInvoked<AReplyMessageHandler>()
                        && c.SagaWasInvoked<ASaga>()
                    )
                    || c.HasFailedMessages();
                })
                .Run();

            var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));


            Assert.True(invokedSaga.IsNew);
            Assert.True(((ASagaData)invokedSaga.SagaData).SomeId == expectedSomeId);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class Context : IntegrationContext
        {

        }

        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyServiceConfiguration, CompletionHandler>>();
            }
        }

        class MyOtherEndpointEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherEndpointEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyOtherServiceConfiguration, CompletionHandler>>();
            }
        }

        class CompletionHandler : IHandleTestCompletion
        {
            public Task OnTestCompleted(RunSummary summary)
            {
                //clean-up transport
                //clean-up storage

                return Task.CompletedTask;
            }
        }
    }
}
