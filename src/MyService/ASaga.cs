using MyMessages.Messages;
using NServiceBus;
using System;
using System.Threading.Tasks;

namespace MyService
{
    public class ASaga : Saga<ASagaData>,
        IAmStartedByMessages<StartASaga>,
        IHandleMessages<CompleteASaga>,
        IHandleTimeouts<ASaga.MyTimeout>
    {
        public Task Handle(StartASaga message, IMessageHandlerContext context)
        {
            return RequestTimeout<MyTimeout>(context, DateTime.UtcNow.AddDays(10));
        }

        public Task Handle(CompleteASaga message, IMessageHandlerContext context)
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        public Task Timeout(MyTimeout state, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ASagaData> mapper)
        {
            mapper.ConfigureMapping<StartASaga>(m => m.SomeId).ToSaga(s => s.SomeId);
            mapper.ConfigureMapping<CompleteASaga>(m => m.SomeId).ToSaga(s => s.SomeId);
        }

        public class MyTimeout { }
    }

    public class ASagaData : ContainSagaData
    {
        public Guid SomeId { get; set; }
    }
}
