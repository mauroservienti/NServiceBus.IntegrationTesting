using MyOtherService;
using MyService.AcceptanceTests.Templates;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using System.Threading.Tasks;
using MyService.AcceptanceTests.Extensions;
using MyMessages.Messages;

namespace MyService.AcceptanceTests
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
                        c.WhenHandlerIsInvoked<MyService.AMessageHandler>()
                        && c.WhenHandlerIsInvoked<MyOtherService.AMessageHandler>()
                    )
                    || c.HasFailedMessages();
                })
                .Run();

            Assert.False(context.HasFailedMessages());
        }

        class Context : SpecialContext
        {
            
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyServiceConfiguration>>();
            }
        }

        class MyOtherEndpointEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherEndpointEndpoint()
            {
                EndpointSetup<ServiceTemplate<MyOtherServiceConfiguration>>();
            }
        }
    }
}
