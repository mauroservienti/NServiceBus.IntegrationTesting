using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public interface IRemoteEndpointWhenDefinition
    {
        Guid Id { get; }

        Task<bool> ExecuteAction(IntegrationScenarioContext context, IRemoteMessageSessionProxy session);
    }
}