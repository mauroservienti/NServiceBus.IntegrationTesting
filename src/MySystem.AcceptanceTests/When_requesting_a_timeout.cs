using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    [TestFixture]
    public class When_requesting_a_timeout
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            await DockerCompose.Up();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            DockerCompose.Down();
        }

        [Test]
        public async Task It_should_be_rescheduled_and_handled()
        {
            var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
            {
                ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>(currentDelay =>
                {
                    return new DoNotDeliverBefore(DateTime.Now.AddSeconds(5));
                });
            })
            .WithEndpoint<MyServiceEndpoint>(g => g.When(session => session.Send("MyService", new StartASaga() { SomeId = Guid.NewGuid() })))
            .Done(c => c.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || c.HasFailedMessages())
            .Run();

            Assert.True(context.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>());
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<EndpointTemplate<MyServiceConfiguration>>();
            }
        }
    }
}
