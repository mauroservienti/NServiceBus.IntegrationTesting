# V1.x and v2.0: in-process with NServiceBus.AcceptanceTesting

> [!NOTE]
> v1.x of the framework has two fundamental limitations:
>
> - All endpoints must share the same NServiceBus package(s) version because they run in-process.
> - NUnit is the only possible testing framework dictated by the NServiceBus.AcceptanceTesting dependency

V1/2 of the framework enables tests like:

```cs
[Test]
public async Task AReplyMessage_is_received_and_ASaga_is_started()
{
    var context = await Scenario.Define<IntegrationScenarioContext>()
        .WithEndpoint<MyServiceEndpoint>(b =>
            b.When(session => session.Send(new AMessage { AnIdentifier = Guid.NewGuid() })))
        .WithEndpoint<MyOtherServiceEndpoint>()
        .Done(c => c.SagaWasInvoked<ASaga>() || c.HasFailedMessages())
        .Run();

    var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));
    Assert.True(invokedSaga.IsNew);
}
```

## V1.x Resources

- [Exploring NServiceBus Integration Testing options](https://milestone.topics.it/2019/07/04/exploring-nservicebus-integration-testing-options.html)
- [NServiceBus.IntegrationTesting baby steps](https://milestone.topics.it/2021/04/07/nservicebus-integrationtesting-baby-steps.html)
