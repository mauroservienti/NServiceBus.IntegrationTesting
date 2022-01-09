using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    public class OutOfProcessEndpointBehaviorBuilder<TContext> where TContext : IntegrationScenarioContext
    {
        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<IRemoteMessageSessionProxy, TContext, Task> action)
        {
            return When(c => true, action);
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<IRemoteMessageSessionProxy, Task> action)
        {
            return When(c => true, action);
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Func<IRemoteMessageSessionProxy, Task> action)
        {
            Whens.Add(new RemoteEndpointWhenDefinition<TContext>(condition, action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IRemoteMessageSessionProxy, Task> action)
        {
            Whens.Add(new RemoteEndpointWhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Func<IRemoteMessageSessionProxy, TContext, Task> action)
        {
            Whens.Add(new RemoteEndpointWhenDefinition<TContext>(condition, action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IRemoteMessageSessionProxy, TContext, Task> action)
        {
            Whens.Add(new RemoteEndpointWhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public IList<IRemoteEndpointWhenDefinition> Whens { get; } = new List<IRemoteEndpointWhenDefinition>();
    }
}