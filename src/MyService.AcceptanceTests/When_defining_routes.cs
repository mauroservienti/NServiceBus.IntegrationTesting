using MyOtherService;
using MyService.AcceptanceTests.Templates;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MyService.AcceptanceTests
{
    public class When_defining_routes
    {
        [Test]
        public async Task route_to_attribute_should_be_overwritten()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<MyServiceEndpoint>(/*g => g.When(b => b.Send(new Message()))*/)
                .WithEndpoint<MyOtherEndpointEndpoint>()
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(context.MessageReceived);
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
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
