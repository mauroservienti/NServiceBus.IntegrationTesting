using MyMessages.Messages;
using NServiceBus;
using System;
using System.Threading.Tasks;

namespace MyService
{
    public class ASaga : Saga<ASagaData>,
        IAmStartedByMessages<StartASaga>,
        IHandleMessages<CompleteASaga>
    {
        public Task Handle(StartASaga message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }

        public Task Handle(CompleteASaga message, IMessageHandlerContext context)
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ASagaData> mapper)
        {
            mapper.ConfigureMapping<StartASaga>(m => m.SomeId).ToSaga(s => s.SomeId);
            mapper.ConfigureMapping<CompleteASaga>(m => m.SomeId).ToSaga(s => s.SomeId);
        }
    }

    public class ASagaData : ContainSagaData
    {
        public Guid SomeId { get; set; }
    }
}
