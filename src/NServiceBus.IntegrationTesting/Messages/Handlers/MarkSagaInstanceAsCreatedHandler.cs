using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Messages.Handlers
{
    class MarkSagaInstanceAsCreatedHandler : IHandleMessages<MarkSagaInstanceAsCreated>
    {
        readonly IntegrationScenarioContext integrationScenarioContext;

        public MarkSagaInstanceAsCreatedHandler(IntegrationScenarioContext integrationScenarioContext)
        {
            this.integrationScenarioContext = integrationScenarioContext;
        }

        public Task Handle(MarkSagaInstanceAsCreated message, IMessageHandlerContext context)
        {
            integrationScenarioContext.RegisterSagaInstanceAsCreated(message.SagaId, message.SagaDataType);

            return Task.CompletedTask;
        }
    }
}
