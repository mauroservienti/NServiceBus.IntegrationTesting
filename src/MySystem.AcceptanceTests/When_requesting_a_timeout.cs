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
    public class When_requesting_a_timeout
    {
        //TODO: renable when a compatible combination of alphas is available
        //[OneTimeSetUp]
        //public async Task Setup()
        //{
        //    await DockerCompose.Up();
        //}

        //[OneTimeTearDown]
        //public void Teardown()
        //{
        //    DockerCompose.Down();
        //}

        [Test]
        public async Task It_should_be_rescheduled_and_handled()
        {
            var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
            {
                ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>((msg, delay) => new DoNotDeliverBefore(DateTime.UtcNow.AddSeconds(5)));
            })
            .WithEndpoint<MyServiceEndpoint>(behavior =>
            {
                behavior.When(session => session.Send("MyService", new StartASaga() {AnIdentifier = Guid.NewGuid()}));
            })
            .Done(ctx => ctx.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || ctx.HasFailedMessages())
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
