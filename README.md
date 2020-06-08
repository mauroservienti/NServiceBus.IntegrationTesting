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
         return c.SagaWasInvoked<ASaga>() || c.HasFailedMessages();
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

When the test is started, it sends an initial `AMessage` message to trigger the choreography, and then it lets the endpoints involved do their job until a specific condition is met. In this sample the `done` condition is quite complex:

- An couple of handlers and a saga need to be invoked
- Or there are failed messages

*NOTE*: Endpoints in the samples contained in this repository are using RabbitMQ as the NServiceBus transport, LearningPersistence as the persistence mechanism. Tests are using docker-compose to make sure the required infrastructure is made available to endpoints exercised by tests.

## How to define a test

Defining an NServiceBus integration test is a multi-step process, composed of:

- Make sure endpoints configuration can be istantiated by tests
- Define endpoints used in each test; and if needed customize each endpoint configuration to adapt to the test environment
- Define tests and completion criteria
- Assert on tests results

### Make sure endpoints configuration can be istantiated by tests

One of the goals of end-to-end testing an NServiceBus endpoints is to make sure that what gets tested is the real production code, not a copy of it crafted for the tests. The production endpoint configuration has to be used in tests. To make sure that the testing infrastructure can instantiate the endpoint configuration there are a couple of options, with many variations.

#### Inherit from EndpointConfiguration

It's possible to create a class that inherits from `EndpointConfiguration` and then use it in both the production endpoint and the tests. To make so that the testing infrastructure con automatically instante it, the class must have a parameterless constructor, like in the following snippet:

```csharp
namespace MyService
{
    public class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            this.SendFailedMessagesTo("error");
            this.EnableInstallers();

            var transportConfig = this.UseTransport<RabbitMQTransport>();
            transportConfig.UseConventionalRoutingTopology();
            transportConfig.ConnectionString("host=localhost");
        }
    }
}
```

Using the above approach can be problematic when configuration values need to be read from an external source, like for example a configuration file. If this is the case the same external configuration source, most of the times with different values, needs to be available in tests too.

#### Use a builder class

In case configuration values need to be passed to the endpoint configuration the easiest option is to use a builder class, even a very simple static one, that can then be used in tests as well with different configuration values. The following snippet shows a simple configuration builder:

```csharp
namespace MyService
{
    public static class MyServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName, string rabbitMqConnectionString)
        {
            var config = new EndpointConfiguration(endpointName);
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();

            var transportConfig = config.UseTransport<RabbitMQTransport>();
            transportConfig.UseConventionalRoutingTopology();
            transportConfig.ConnectionString(rabbitMqConnectionString);
            
            return config;
        }
    }
}
```

### Define endpoints used in each test

To define an endpoint in tests a class inheriting from `EndpointConfigurationBuilder` needs to be created for each endpoint that needs to be used in a test. The best place to define such classes is as nested classes within the test class itself:

```csharp
public class When_sending_AMessage
{
    class MyServiceEndpoint : EndpointConfigurationBuilder
    {
        public MyServiceEndpoint()
        {
            EndpointSetup<EndpointTemplate<MyServiceConfiguration>>();
        }
    }

    class MyOtherServiceEndpoint : EndpointConfigurationBuilder
    {
        public MyOtherServiceEndpoint()
        {
            EndpointSetup<MyOtherServiceTemplate>();
        }
    }
}
```

The sample defines two endpoints, `MyServiceEndpoint` and `MyOtherServiceEndpoint`. `MyServiceEndpoint` uses the "inherit from EndpointConfiguration" approach to reference the production endpoint configuration. `MyOtherServiceEndpoint` uses the "builder class" by creating a custom endpoint template:

```csharp
class MyOtherServiceTemplate : EndpointTemplate
{
    protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
    {
        var config = MyOtherServiceConfigurationBuilder.Build(
            "MyOtherService",
            "host=localhost;username=guest;password=guest");
        return Task.FromResult(config);
    }
}
```

Using both approaches the endpoint configuration can be customized according to the environment needs, if needed.

### Define tests and completion criteria

#### Scenario

Once endpoints are defined, the test choreography can be implemented, the first thing is to define a `Scenario`:

```csharp
public class When_sending_AMessage
{
    [Test]
    public async Task AReplyMessage_is_received_and_ASaga_is_started()
    {
        var context = await Scenario.Define<IntegrationScenarioContext>()
            .WithEndpoint<MyServiceEndpoint>(...)
            .WithEndpoint<MyOtherServiceEndpoint>(...)
            .Done(...)
            .Run();
    }

    class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
    class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
}
```

NOTE: The defined `Scenario` must use the `InterationScenarioContext` or a type that inherits from `InterationScenarioContext`.

This tests aims to verify that when "MyService" sends a message to "MyOtherService" a reply is received by "MyService" and finally that a new saga instance is created. 

```csharp
var context = await Scenario.Define<IntegrationScenarioContext>()
   .WithEndpoint<MyServiceEndpoint>(...)
   .WithEndpoint<MyOtherServiceEndpoint>(...)
   .Done(...)
   .Run();
```

Use the `Define` static method to create a scenario and then add endpoints to the created scenario to append as many endpoints as needed for the scenario. Add a `Done` condition to specify when the test has to be considered completed adn finally invoke `Run` to exercise the `Scenario`.

#### Done condition

An end-to-end test execution can only be terminated by 3 events:

- the done condition is `true`
- the test times out
- there are unhandled exceptions

Unhandled exceptions are a sort of problem from the integration testing infrastructure perspecive as most of the times they'll result in messages being retried and eventually ending up in the error queue. based on this it's better to consider failed messages as part of the done condition:

```csharp
.Done(c =>
{
   return c.HasFailedMessages();
})
```

Such a done condition has to be read has: "If there are one or more failed messages the test is done, proceed to evaulate the assertions". Obviously this is not enough. In the identified test case scenario the test is done when a saga is invoked (specifically is created, more on this later). A saga invokation can be expressed as a done condition in the following way:

```csharp
.Done(c =>
{
   return c.SagaWasInvoked<ASaga>() || c.HasFailedMessages();
})
```

The integration scenario context, the `c` argument, can be "queried" to gather the status of the test, in this case the done condition is augmented to make so that the test is considered done when a saga of type `ASaga` has been invoked or there are failed messages.

#### Kick-off the test choreography 

The last bit is to kick-off the choreography to test. This is usually done by stimulating the system with a message. `WithEndpoint<T>` has an overload that allows to define a callback that is invoked when the test is run.
In the defined callback it's possible to define one or more "when" conditions that are evaluated by the testing infrastructure and invoked at the appropriate time:

```csharp
var context = await Scenario.Define<IntegrationScenarioContext>()
   .WithEndpoint<MyServiceEndpoint>(g => g.When(session => session.Send(new AMessage())))
   .WithEndpoint<MyOtherServiceEndpoint>()
   .Done(...)
   .Run();
```

The above code snippet makes so that when "MyServiceEndpoint" is started `AMessage` is sent. `When` has multiple overloads to accommodate many different scenarios.

### Assert on tests results

The last step is to assert on test results. The `IntegrationScenarioContext` instance, created at the beginning of the test captures tests stages and exposes an API to query tests results. The following snippet demonstrates how to assert that a new `ASaga` instance has been created with specific values, a `AMessage` has been handled, and `AReplyMessage` has been handled:

```csharp
var invokedSaga = context.InvokedSagas.SingleOrDefault(s => s.SagaType == typeof(ASaga));

Assert.IsNotNull(invokedSaga);
Assert.True(invokedSaga.IsNew);
Assert.AreEqual("MyService", invokedSaga.EndpointName);

Assert.True(context.HandlerWasInvoked<AMessageHandler>());
Assert.True(context.HandlerWasInvoked<AReplyMessageHandler>());

Assert.False(context.HasFailedMessages());
Assert.False(context.HasHandlingErrors());
```

The context contains also general assertions like `HasFailedMessages` or `HasHandlingErrors` useful to validate the overall correctness of the test execution. `HasFailedMessages` Is `true` if any message has been moved to the configured error queue. `HasHandlingErrors` is `true` if any handler or saga invocation failed at least ones.

## How to deal with NServiceBus Timeouts

When testing production code, running a choreography, NServiceBus Timeouts can be problematic. Tests will timeout after 90 seconds, this means that if the production code schedules an NServiceBus Timeout for 1 hour, or for next week, it becomes impossible to verify the choreography.

NServiceBus.IntegrationTesting provides a way to reschedule NServiceBus Timeouts when they are requested by the production code:

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

The above sample test shows how to inject an NServiceBus Timeout reschedule rule. When the production code, in this case the `ASaga` saga, schedules the `ASaga.MyTimeout` message, the registered NServiceBus Timeout reschedule rule will be invoked and a new delivery constraint is created, in this sample, to make so that the NServiceBus Timeout expires in 5 seconds insted of the default production value. The NServiceBus Timeout reschedule rule receives as arguments the current NServiceBus Timeout message and the current delivery constraint.

## Limitations

NServiceBus.IntegrationTesting is built on top of the NServiceBus.AcceptanceTesting framework, this comes with a few limitations:

- Each test has a fixed, hardcoded, timeout of 90 seconds
- Tests can only use NUnit and at this time there is no way to use a different unit testing framework
- All endpoints run in a test share the same test process, they are not isolated

## How to install

Using a package manager add a nuget reference to [NServiceBus.IntegrationTesting](https://www.nuget.org/packages/NServiceBus.IntegrationTesting/).

## Background

For more information on the genesis of this package refer to the [Exploring NServiceBus Integration Testing options](https://milestone.topics.it/2019/07/04/exploring-nservicebus-integration-testing-options.html) article.

---

Icon [test](https://thenounproject.com/search/?q=test&i=2829166) by Andrei Yushchenko from the Noun Project
