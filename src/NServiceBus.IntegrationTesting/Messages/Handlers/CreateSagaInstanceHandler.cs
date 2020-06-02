using NServiceBus.AcceptanceTesting;
using NServiceBus.Sagas;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Messages.Handlers
{
    class CreateSagaInstanceHandler : IHandleMessages<CreateSagaInstance>
    {
        readonly ISagaPersister sagaPersister;

        public CreateSagaInstanceHandler(ISagaPersister sagaPersister)
        {
            this.sagaPersister = sagaPersister;
        }

        public async Task Handle(CreateSagaInstance message, IMessageHandlerContext context)
        {
            var correlationProperty = new SagaCorrelationProperty(
                message.CorrelationPropertyName, 
                message.CorrelationPropertyValue);

            await sagaPersister.Save(message.SagaData, 
                correlationProperty, 
                context.SynchronizedStorageSession, 
                new Extensibility.ContextBag())
                .ConfigureAwait(false);

            await context.SendLocal(new MarkSagaInstanceAsCreated() 
            {
                SagaId = message.SagaData.Id,
                SagaDataType = message.SagaData.GetType()
            }).ConfigureAwait(false);
        }
    }
}
