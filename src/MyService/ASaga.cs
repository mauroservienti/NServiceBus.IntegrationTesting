using MyMessages.Messages;
using NServiceBus;
using System;
using System.Threading.Tasks;

namespace MyService
{
    public class ASaga : Saga<ASagaData>,
        IAmStartedByMessages<StartASaga>
    {
        public Task Handle(StartASaga message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ASagaData> mapper)
        {
            mapper.ConfigureMapping<StartASaga>(m => m.SomeId).ToSaga(s => s.SomeId);
        }
    }

    public class ASagaData : ContainSagaData
    {
        public Guid SomeId { get; set; }
    }
}
