using NServiceBus;
using SampleMessages;

namespace SampleEndpoint.Handlers;

class SomeReplySaga :
    Saga<SomeReplySagaData>,
    IAmStartedByMessages<SomeReply>,
    IHandleTimeouts<SomeReplySagaTimeout>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SomeReplySagaData> mapper)
    {
        mapper.MapSaga(saga => saga.SomeMessageCorrelationId)
            .ToMessage<SomeReply>(msg => msg.CorrelationId);
    }

    public async Task Handle(SomeReply message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SomeReplySaga] Started by SomeReply {message.CorrelationId}, requesting 20s timeout");
        await RequestTimeout<SomeReplySagaTimeout>(context, TimeSpan.FromMinutes(20));
    }

    public async Task Timeout(SomeReplySagaTimeout state, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SomeReplySaga] Timeout fired for {Data.SomeMessageCorrelationId}, sending SagaCompletedMessage");
        await context.SendLocal(new SagaCompletedMessage { SagaCorrelationId = Data.SomeMessageCorrelationId });
        MarkAsComplete();
    }
}
