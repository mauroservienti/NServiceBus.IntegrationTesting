<img src="assets/icon.png" width="100" />

# NServiceBus.IntegrationTesting

NServiceBus.IntegrationTesting allows testing end-to-end business scenarios, exercising the real production code.

## tl;dr

NServiceBus.IntegrationTesting enables a test like the following one to be defined:

```csharp
[Test]
public async Task AReplyMessage_is_received_and_ASaga_is_started()
{
   var theExpectedSagaId = Guid.NewGuid();
   var context = await Scenario.Define<IntegrationScenarioContext>()
      .WithEndpoint<MyServiceEndpoint>(g => g.When(b => b.Send(new AMessage() { ThisWillBeTheSagaId = theExpectedSagaId })))
      .WithEndpoint<MyOtherServiceEndpoint>()
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
      Assert.AreEqual("MyService", invokedSaga.EndpointName);
      Assert.True(((ASagaData)invokedSaga.SagaData).SomeId == theExpectedSagaId);
      Assert.False(context.HasFailedMessages());
      Assert.False(context.HasHandlingErrors());
}
```

(Full test [source code](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/src/MySystem.AcceptanceTests/When_sending_AMessage.cs) for the above sample is available in this repo)

The above test does quite a lot of things, but the most important ones are:

- it's exercising the real production endpoints
- it's asserting on the end-to-end choreography, for example it's checking that a saga was invoked and/or a message handler was invoked

When the test is started, it sends an initial `AMessage` message to trigger the choreography, and then it lets the endpoints involved do their job untill a specific condition is met. In this sample the `done` condition is quite complex:

- An couple of handlers and a saga need to be invoked
- Or there are failed messages

*NOTE*: Endpoints in the samples contained in this repository are using RabbitMQ as the NServiceBus transport, LearningPersistence as the persistence mechanism. Tests are using docker-compose to make sure the required infrastructure is made available to endpoints exercised by tests.

## How to define a test

## Available assertions

## How to deal with timeouts

When testing production code, running a choreography, timeouts can be problematic. Tests timeout after 90 seconds, this means that if the production code schedules a timeouts for 1 hour, or for next week, it becomes essentially impossible to verify the choreography.

NServiceBus.IntegrationTesting provides a way to reschedule timeouts when they are requested by the production code:

```csharp
var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
{
    ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>((message, currentDelay) =>
    {
        return new DoNotDeliverBefore(DateTime.UtcNow.AddSeconds(5));
    });
})
.WithEndpoint<MyServiceEndpoint>(g => g.When(session => session.Send("MyService", new StartASaga() { SomeId = Guid.NewGuid() })))
.Done(c => c.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || c.HasFailedMessages())
.Run();
```

The above sample test shows how to inject a timeout reschedule rule. When the production code, in this case the `ASaga` saga, schedules the `ASaga.MyTimeout` message, the registered timeout reschedule rule will be invoked and a new delivery constraint is created, in this sample, to make so that the timeout expires in 5 seconds insted of the default production value. The timeout reschedule rule receives as arguments the current timeout message and the current delivery constraint.

## Limitations

NServiceBus.IntegrationTesting is built on top of the NServiceBus.AcceptanceTesting framework, this comes with a few limitations:

- Each test has a fixed, hardcoded, timeout of 90 seconds
- Tests can only use NUnit and at this time there is no way to use a different unit testing framework

## How to install

Using a package manager add a nuget reference to [NServiceBus.IntegrationTesting](https://www.nuget.org/packages/NServiceBus.IntegrationTesting/).

## Background

For more information on the genesis of this package refer to the [Exploring NServiceBus Integration Testing options](https://milestone.topics.it/2019/07/04/exploring-nservicebus-integration-testing-options.html) article.

---

Icon [test](https://thenounproject.com/search/?q=test&i=2829166) by Andrei Yushchenko from the Noun Project
