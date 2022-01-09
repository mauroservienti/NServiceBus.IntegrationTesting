using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    class RemoteEndpointWhenDefinition<TContext> : IRemoteEndpointWhenDefinition where TContext : IntegrationScenarioContext
    {
        public RemoteEndpointWhenDefinition(Func<TContext, Task<bool>> condition, Func<IRemoteMessageSessionProxy, Task> action)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            messageAction = action;
        }

        public RemoteEndpointWhenDefinition(Func<TContext, Task<bool>> condition, Func<IRemoteMessageSessionProxy, TContext, Task> actionWithContext)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            messageAndContextAction = actionWithContext;
        }

        public Guid Id { get; }

        public async Task<bool> ExecuteAction(IntegrationScenarioContext context, IRemoteMessageSessionProxy session)
        {
            var c = (TContext)context;

            if (!await condition(c).ConfigureAwait(false))
            {
                return false;
            }

            if (messageAction != null)
            {
                await messageAction(session).ConfigureAwait(false);
            }
            else
            {
                await messageAndContextAction(session, c).ConfigureAwait(false);
            }

            return true;
        }

        Func<TContext, Task<bool>> condition;
        Func<IRemoteMessageSessionProxy, Task> messageAction;
        Func<IRemoteMessageSessionProxy, TContext, Task> messageAndContextAction;
    }
}