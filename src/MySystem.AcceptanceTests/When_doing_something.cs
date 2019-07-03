using MyMessages.Messages;
using MyOtherService;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_doing_something
    {
        [Test]
        public async Task something_should_happen()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<MyServiceEndpoint>(g => g.When(b => b.Send(new AMessage())))
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

            Assert.True(context.SagaWasInvoked<ASaga>());
            Assert.True(context.HandlerWasInvoked<AMessageHandler>());
            Assert.True(context.HandlerWasInvoked<AReplyMessageHandler>());
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
                EndpointSetup<ServiceTemplate<MyServiceConfiguration, EmptyTestCompletionHandler>>();
            }
        }

        class MyOtherEndpointEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherEndpointEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyOtherServiceConfiguration, EmptyTestCompletionHandler >> ();
            }
        }
    }
}
