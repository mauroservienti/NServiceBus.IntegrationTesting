using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    public class OutOfProcessEndpointBehaviorBuilder<TContext> where TContext : ScenarioContext
    {
        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<IMessageSession, TContext, Task> action)
        {
            return When(c => true, action);
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<IMessageSession, Task> action)
        {
            return When(c => true, action);
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Func<IMessageSession, Task> action)
        {
            Whens.Add(new WhenDefinition<TContext>(condition, action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IMessageSession, Task> action)
        {
            Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Func<IMessageSession, TContext, Task> action)
        {
            Whens.Add(new WhenDefinition<TContext>(condition, action));

            return this;
        }

        public OutOfProcessEndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IMessageSession, TContext, Task> action)
        {
            Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public IList<IWhenDefinition> Whens { get; } = new List<IWhenDefinition>();
    }
}