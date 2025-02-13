<img src="assets/icon.png" width="100" />

# NServiceBus.IntegrationTesting

NServiceBus.IntegrationTesting allows testing end-to-end business scenarios, exercising the real production code.

## Disclaimer

NServiceBus.IntegrationTesting is not affiliated with Particular Software and thus is not officially supported. Its evolution stage doesn't make it production-ready yet.

## tl;dr

NServiceBus.IntegrationTesting enables a test like the following one to be defined:

<!-- snippet: too-long-dont-read-full-test -->
<a id='snippet-too-long-dont-read-full-test'></a>
```cs
[Test]
public async Task AReplyMessage_is_received_and_ASaga_is_started()
{
    var theExpectedIdentifier = Guid.NewGuid();
    var context = await Scenario.Define<IntegrationScenarioContext>()
        .WithEndpoint<MyServiceEndpoint>(behavior =>
        {
            behavior.When(session => session.Send(new AMessage() {AnIdentifier = theExpectedIdentifier}));
        })
        .WithEndpoint<MyOtherServiceEndpoint>()
        .Done(c => c.SagaWasInvoked<ASaga>() || c.HasFailedMessages())
        .Run();

    var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

    Assert.True(invokedSaga.IsNew);
    Assert.AreEqual("MyService", invokedSaga.EndpointName);
    Assert.True(((ASagaData)invokedSaga.SagaData).AnIdentifier == theExpectedIdentifier);
    Assert.False(context.HasFailedMessages());
    Assert.False(context.HasHandlingErrors());
}
```
<sup><a href='/src/Snippets/TooLongDontReadSnippets.cs#L15-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-too-long-dont-read-full-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

(Full test [source code](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/src/MySystem.AcceptanceTests/When_sending_AMessage.cs) for the above sample is available in this repo)

The above test does quite a lot of things, but the most important ones are:

- it's exercising the real production endpoints
- it's asserting on the end-to-end choreography; for example, it's checking that a saga was invoked and/or a message handler was invoked

When the test is started, it sends an initial `AMessage` message to trigger the choreography, and then it lets the endpoints involved do their job until a specific condition is met. In this example, the `done` condition is:

- A saga is invoked
- Or there are failed messages

> [!NOTE]
> Endpoints in the samples use RabbitMQ as the NServiceBus transport and LearningPersistence as the persistence mechanism. Tests use `docker-compose` to ensure the required infrastructure is made available to endpoints exercised by tests.

## Why?

By using [NServiceBus testing](https://docs.particular.net/nservicebus/testing/) capabilities it is possible to unit test all NServiceBus features, from message handlers and sagas to custom behaviors. This doesn't mean that the system will work as expected in production, things like:

- the overall business process behavior or choreography  
- correctness of the endpoint configuration
- message routing and events subscriptions
- saga message mappings

and probably more, cannot be tested using a unit testing approach. NServiceBus.IntegrationTesting is designed to solve these problems.

### Testing business processes (choreography testing)

The goal of choreography testing is to consider endpoints in the system as black boxes. The only things the test knows are input messages, output messages, and if really needed, saga data stored by NServiceBus sagas while processing messages. In this case, saga data are considered an output of an endpoint.

### Testing endpoint configuration

It's important to ensure that each endpoint configuration works as expected. The only real way to validate that is to exercise the real production endpoint by instantiating and running it using the exact same configuration that will be used in production.

### Testing message routing

[Message routing](https://docs.particular.net/nservicebus/messaging/routing) is part of the endpoint configuration and is what makes a choreography successful; the only way to test the correctness of the message routing setup is to exercise the choreography so that endpoints will use the production routing configuration to exchange messages, and to subscribe to events. If the routing configuration is wrong or is missing pieces, the choreography tests will fail.

### Testing saga message mappings

Theoretically, asserting on [saga message mappings](https://docs.particular.net/nservicebus/sagas/message-correlation) using an approval testing approach is possible. By using this technique, it's possible to catch changes in mappings. Although, it's impossible to validate that mappings are correct. The only way to make sure saga mappings are correct is, once again, to exercise sagas by hosting them in endpoints, by sending messages and asserting on published messages and saga data.

## How to define a test

Defining an NServiceBus integration test is a multi-step process composed of the following:

- Make sure endpoint configuration can be instantiated by tests
- Define endpoints used in each test, and if needed, customize each endpoint configuration to adapt to the test environment
- Define tests and completion criteria
- Assert on tests results

### Make sure endpoint configuration can be instantiated by tests

One of the goals of end-to-end testing an NServiceBus endpoint is to ensure that what gets tested is the real production code, not a copy of it crafted for the tests. The production endpoint configuration has to be used in tests. To ensure that the testing infrastructure can instantiate the endpoint configuration, there are a couple of options, with many variations.

#### Inherit from EndpointConfiguration

It's possible to create a class that inherits from `EndpointConfiguration` and then use it in both the production endpoint and the tests. To make it so that the testing infrastructure can automatically instantiate it, the class must have a parameterless constructor, like in the following snippet:

<!-- snippet: inherit-from-endpoint-configuration -->
<a id='snippet-inherit-from-endpoint-configuration'></a>
```cs
public class MyServiceConfiguration : EndpointConfiguration
{
    public MyServiceConfiguration()
        : base("MyService")
    {
        this.SendFailedMessagesTo("error");
        this.EnableInstallers();

        this.UseTransport(new RabbitMQTransport(Topology.Conventional, "host=localhost"));
    }
}
```
<sup><a href='/src/Snippets/ConfigurationSnippets.cs#L5-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-inherit-from-endpoint-configuration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Using the above approach can be problematic when configuration values need to be read from an external source, like a configuration file. If this is the case, the same external configuration source, most of the time with different values, needs to be available in tests, too.

#### Use a builder class

In case configuration values need to be passed to the endpoint configuration the easiest option is to use a builder class, even a very simple static one, that can then be used in tests with different configuration values. The following snippet shows a simple configuration builder:

<!-- snippet: use-builder-class -->
<a id='snippet-use-builder-class'></a>
```cs
public static class MyServiceConfigurationBuilder
{
    public static EndpointConfiguration Build(string endpointName, string rabbitMqConnectionString)
    {
        var config = new EndpointConfiguration(endpointName);
        config.SendFailedMessagesTo("error");
        config.EnableInstallers();

        config.UseTransport(new RabbitMQTransport(Topology.Conventional, rabbitMqConnectionString));

        return config;
    }
}
```
<sup><a href='/src/Snippets/ConfigurationSnippets.cs#L19-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-builder-class' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Define endpoints used in each test

To define an endpoint in tests, a class inheriting from `NServiceBus.AcceptanceTesting.EndpointConfigurationBuilder` needs to be created for each endpoint that needs to be used in a test. The best place to define such classes is as nested classes within the test class itself:

<!-- snippet: endpoints-used-in-each-test -->
<a id='snippet-endpoints-used-in-each-test'></a>
```cs
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
<sup><a href='/src/Snippets/EndpointsSnippets.cs#L11-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-endpoints-used-in-each-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample defines two endpoints, `MyServiceEndpoint` and `MyOtherServiceEndpoint`. `MyServiceEndpoint` uses the "inherit from EndpointConfiguration" approach to reference the production endpoint configuration. `MyOtherServiceEndpoint` uses the "builder class" by inheriting from `NServiceBus.IntegrationTesting.EndpointTemplate`:

<!-- snippet: my-other-service-template -->
<a id='snippet-my-other-service-template'></a>
```cs
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
<sup><a href='/src/Snippets/EndpointsSnippets.cs#L37-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-my-other-service-template' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The "builder class" approach allows specific modifications of the `EndpointConfiguration` for the tests. Although modifications should be kept to a minimum, they are reasonable for a few aspects:

- Retries
  - Retries should be reduced or even disabled for tests.
  - Otherwise (with the default retry configuration), the IntegrationTests throw a "Some failed messages were not handled by the recoverability feature."-Exception because of a fixed 30-sec timeout in the `NServiceBus.AcceptanceTests.ScenarioRunner`.
- Cleaning up the queues via `PurgeOnStartup(true)`

#### Generic host support

NServiceBus endpoints can be [hosted using the generic host](https://docs.particular.net/samples/hosting/generic-host/). When using the generic host, the endpoint lifecycle and configuration are controlled by the host. The following is a sample endpoint hosted using the generic host:

<!-- snippet: basic-generic-host-endpoint -->
<a id='snippet-basic-generic-host-endpoint'></a>
```cs
public static void Main(string[] args)
{
    CreateHostBuilder(args).Build().Run();
}

public static IHostBuilder CreateHostBuilder(string[] args)
{
    var builder = Host.CreateDefaultBuilder(args);
    builder.UseConsoleLifetime();

    builder.UseNServiceBus(ctx =>
    {
        var config = new EndpointConfiguration("endpoint-name");
        config.UseTransport(new LearningTransport());

        return config;
    });

    return builder;
}
```
<sup><a href='/src/Snippets/GenericHostSnippets.cs#L27-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-basic-generic-host-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> For more information about hosting NServiceBus using the generic host, refer to the [official documentation](https://docs.particular.net/samples/hosting/generic-host/). 

Before using the generic host, hosted endpoints with `NServiceBus.IntegrationTesting`, a minor change to the above snippet is required:

<!-- snippet: basic-generic-host-endpoint-with-config-previewer -->
<a id='snippet-basic-generic-host-endpoint-with-config-previewer'></a>
```cs
public static IHostBuilder CreateHostBuilder(string[] args, Action<EndpointConfiguration> configPreview)
{
    var builder = Host.CreateDefaultBuilder(args);
    builder.UseConsoleLifetime();

    builder.UseNServiceBus(ctx =>
    {
        var config = new EndpointConfiguration("endpoint-name");
        config.UseTransport(new LearningTransport());

        configPreview?.Invoke(config);
        
        return config;
    });

    return builder;
}
```
<sup><a href='/src/Snippets/GenericHostSnippets.cs#L50-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-basic-generic-host-endpoint-with-config-previewer' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The testing engine needs to access the endpoint configuration before it's initialized to register the needed tests behaviors. The creation of the `IHostBuilder` needs to be tweaked to invoke a callback delegate that the test engine injects at test runtime. 

Finally, the endpoint can be added to the scenario using the `WithGenericHostEndpoint` configuration method:

<!-- snippet: with-generic-host-endpoint -->
<a id='snippet-with-generic-host-endpoint'></a>
```cs
_ = await Scenario.Define<IntegrationScenarioContext>()
    .WithGenericHostEndpoint("endpoint-name", configPreview => Program.CreateHostBuilder(new string[0], configPreview).Build())
```
<sup><a href='/src/Snippets/GenericHostSnippets.cs#L16-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-with-generic-host-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Be sure to pass to the method that creates the `IHostBuilder` the provided `Action<EndpointConfiguration>` parameter. If the endpoint is not configured correctly the following exception will be raised at test time:

> Endpoint \<endpointName\> is not correctly configured to be tested. Make sure to pass the EndpointConfiguration instance to the Action<EndpointConfiguration> provided by the `WithGenericHostEndpoint` tests setup method.

### Define tests and completion criteria

#### Scenario

Once endpoints are defined, the test choreography can be implemented; the first thing is to define a `Scenario`:

<!-- snippet: scenario-skeleton -->
<a id='snippet-scenario-skeleton'></a>
```cs
[Test]
public async Task AReplyMessage_is_received_and_ASaga_is_started()
{
    var context = await Scenario.Define<IntegrationScenarioContext>()
        .WithEndpoint<MyServiceEndpoint>(/*...*/)
        .WithEndpoint<MyOtherServiceEndpoint>(/*...*/)
        .Done(ctx=>false)
        .Run();
}

class MyServiceEndpoint : EndpointConfigurationBuilder{ /* omitted */ }
class MyOtherServiceEndpoint : EndpointConfigurationBuilder{ /* omited */ }
```
<sup><a href='/src/Snippets/ScenarioSnippets.cs#L10-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-scenario-skeleton' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

NOTE: The defined `Scenario` must use the `IntegrationScenarioContext` or a type that inherits from `IntegrationScenarioContext`.

This test aims to verify that when "MyService" sends a message to "MyOtherService," a reply is received by "MyService," and finally a new saga instance is created. Use the `Define` static method to create a scenario and then add endpoints to the created scenario to append as many endpoints as needed for the scenario. Add a `Done` condition to specify when the test must be completed and finally invoke `Run` to exercise the `Scenario`.

#### Done condition

An end-to-end test execution can only be terminated by three events:

- the done condition is `true`
- the test times out
- there are unhandled exceptions

Unhandled exceptions are a sort of problem from the integration testing infrastructure perspective as, most of the time; they'll result in messages being retried and eventually ending up in the error queue. Based on this, it's better to consider failed messages as part of the done condition:

<!-- snippet: simple-done-condition -->
<a id='snippet-simple-done-condition'></a>
```cs
.Done(ctx =>
{
    return ctx.HasFailedMessages();
})
```
<sup><a href='/src/Snippets/DoneSnippets.cs#L15-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-simple-done-condition' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Such a done condition has to be read as: "If there are one or more failed messages the test is done, proceed to evaulate the assertions". More is needed. The test is done in the identified test case scenario when a saga is invoked (specifically, it is created; more on this later). A saga invocation can be expressed as a done condition in the following way:

<!-- snippet: complete-done-condition -->
<a id='snippet-complete-done-condition'></a>
```cs
.Done(ctx =>
{
    return ctx.SagaWasInvoked<ASaga>() || ctx.HasFailedMessages();
})
```
<sup><a href='/src/Snippets/DoneSnippets.cs#L26-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-complete-done-condition' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The integration scenario context, the `c` argument, can be "queried" to gather the status of the test, in this case the done condition is augmented to make so that the test is considered done when a saga of type `ASaga` has been invoked or there are failed messages.

#### Kick off the test choreography

The last bit is to kick off the choreography to test. This is usually done by stimulating the system with a message. `WithEndpoint<T>` has an overload that allows the definition of a callback invoked when the test is run.
In the defined callback, it's possible to define one or more "when" conditions that are evaluated by the testing infrastructure and invoked at the appropriate time:

<!-- snippet: kick-off-choreography -->
<a id='snippet-kick-off-choreography'></a>
```cs
var context = await Scenario.Define<IntegrationScenarioContext>()
    .WithEndpoint<MyServiceEndpoint>(builder => builder.When(session => session.Send(new AMessage())))
```
<sup><a href='/src/Snippets/KickOffSnippets.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-kick-off-choreography' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The above code snippet makes sure that when "MyServiceEndpoint" is started, `AMessage` is sent. `When` has multiple overloads (including one with a condition parameter) to accommodate many different scenarios.

### Assert on tests results

The last step is to assert on test results. The `IntegrationScenarioContext` instance, created at the beginning of the test, captures test stages and exposes an API to query test results. The following snippet demonstrates how to assert that a new `ASaga` instance has been created with specific values, an `AMessage` has been handled, and `AReplyMessage` has been handled:

<!-- snippet: assert-on-tests-results -->
<a id='snippet-assert-on-tests-results'></a>
```cs
var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

Assert.True(invokedSaga.IsNew);
Assert.AreEqual("MyService", invokedSaga.EndpointName);
Assert.True(((ASagaData)invokedSaga.SagaData).AnIdentifier == theExpectedIdentifier);
Assert.False(context.HasFailedMessages());
Assert.False(context.HasHandlingErrors());
```
<sup><a href='/src/Snippets/AssertionSnippets.cs#L27-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-assert-on-tests-results' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The context also contains general assertions like `HasFailedMessages` or `HasHandlingErrors`, which are useful for validating the overall correctness of the test execution. `HasFailedMessages` is `true` if any message has been moved to the configured error queue. `HasHandlingErrors` is `true` if any handler or saga invocation fails at least once.

## How to deal with NServiceBus Timeouts

When testing production code and running a choreography, NServiceBus Timeouts can be problematic. Tests will timeout after 90 seconds. This means that if the production code schedules an NServiceBus Timeout for one hour or for the next week, it becomes impossible to verify the choreography.

NServiceBus.IntegrationTesting provides a way to reschedule NServiceBus Timeouts when they are requested by the production code:

<!-- snippet: timeouts-reschedule -->
<a id='snippet-timeouts-reschedule'></a>
```cs
var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
    {
        ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>((msg, delay) =>
        {
            return new DoNotDeliverBefore(DateTime.UtcNow.AddSeconds(5));
        });
    })
    .WithEndpoint<MyServiceEndpoint>(builder => builder.When(session => session.Send("MyService", new StartASaga() { AnIdentifier = Guid.NewGuid() })))
    .Done(ctx => ctx.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || ctx.HasFailedMessages())
    .Run();
```
<sup><a href='/src/Snippets/TimeoutsRescheduleSnippets.cs#L16-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-timeouts-reschedule' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The above sample test shows how to inject an NServiceBus Timeout reschedule rule. When the production code, in this case, the `ASaga` saga, schedules the `ASaga.MyTimeout` message, the registered NServiceBus Timeout reschedule rule will be invoked, and a new delivery constraint will be created, in this sample, to make the NServiceBus Timeout expire in 5 seconds instead of the default production value. The NServiceBus Timeout reschedule rule receives as arguments the current NServiceBus Timeout message and the current delivery constraint.

## Limitations

NServiceBus.IntegrationTesting is built on top of the NServiceBus.AcceptanceTesting framework, this comes with a few limitations:

- Each test has a fixed, hardcoded timeout of 90 seconds
- Tests can only use NUnit, and at this time, there is no way to use a different unit testing framework
- All endpoints run in a test share the same test process; they are not isolated

### Assembly scanning setup

By default, NServiceBus endpoints scan and load all assemblies in the bin directory. This means that if more than one endpoint is loaded into the same process, all endpoints will scan the same bin directory, and all types related to NServiceBus, such as message handlers and/or sagas, are loaded by all endpoints. This can cause issues with endpoints running in end-to-end tests. It's suggested that the endpoint configuration be configured to scan only a limited set of assemblies and exclude those unrelated to the current endpoint. The assembly scanner configuration can be applied directly to the production endpoint configuration or as a customization in the test endpoint template setup.

<!-- snippet: assembly-scanner-config -->
<a id='snippet-assembly-scanner-config'></a>
```cs
public class MyServiceConfiguration : EndpointConfiguration
{
    public MyServiceConfiguration()
        : base("MyService")
    {
        var scanner = this.AssemblyScanner();
        scanner.IncludeOnly("MyService.dll", "MyMessages.dll");

        //rest of the configuration
    }
}
```
<sup><a href='/src/Snippets/AssemblyScannerSnippets.cs#L5-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-assembly-scanner-config' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `IncludeOnly` extension method is a custom extension defined as follows:

<!-- snippet: include-only-extension -->
<a id='snippet-include-only-extension'></a>
```cs
public static AssemblyScannerConfiguration IncludeOnly(this AssemblyScannerConfiguration configuration, params string[] assembliesToInclude)
{
    var excluded = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
        .Select(path => Path.GetFileName(path))
        .Where(existingAssembly => !assembliesToInclude.Contains(existingAssembly))
        .ToArray();

    configuration.ExcludeAssemblies(excluded);

    return configuration;
}
```
<sup><a href='/src/NServiceBus.AssemblyScanner.Extensions/IncludeOnlyExtension.cs#L9-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-include-only-extension' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The above extension method is far from complete and doesn't handle all the possible scenarios. It's provided as a sample and needs to be customized on a case-by-case basis.

## How to install

Using a package manager, add a Nuget reference to [NServiceBus.IntegrationTesting](https://www.nuget.org/packages/NServiceBus.IntegrationTesting/).

## Background

For more information on the genesis of this package, refer to the [Exploring NServiceBus Integration Testing options](https://milestone.topics.it/2019/07/04/exploring-nservicebus-integration-testing-options.html) article.

---

Icon [test](https://thenounproject.com/search/?q=test&i=2829166) by Andrei Yushchenko from the Noun Project
