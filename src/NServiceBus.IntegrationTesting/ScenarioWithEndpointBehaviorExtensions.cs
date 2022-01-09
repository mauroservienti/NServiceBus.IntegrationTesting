using System;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    public static class ScenarioWithEndpointBehaviorExtensions
    {
        public static IScenarioWithEndpointBehavior<TContext> WithGenericHostEndpoint<TContext>(this IScenarioWithEndpointBehavior<TContext> scenarioWithEndpoint,
            string endpointName, Func<Action<EndpointConfiguration>, IHost> hostBuilder, Action<GenericHostEndpointBehaviorBuilder<TContext>> behavior = null) where TContext : ScenarioContext
        {
            var behaviorBuilder = new GenericHostEndpointBehaviorBuilder<TContext>();
            behavior?.Invoke(behaviorBuilder);

            scenarioWithEndpoint.WithComponent(new GenericHostEndpointBehavior(endpointName, hostBuilder, behaviorBuilder.Whens));

            return scenarioWithEndpoint;
        }

        public static IScenarioWithEndpointBehavior<TContext> WithOutOfProcessEndpoint<TContext>(this IScenarioWithEndpointBehavior<TContext> scenarioWithEndpoint, string endpointName, EndpointReference endpointReference, Action<OutOfProcessEndpointBehaviorBuilder<TContext>> behavior = null) where TContext : IntegrationScenarioContext
        {
            var behaviorBuilder = new OutOfProcessEndpointBehaviorBuilder<TContext>();
            behavior?.Invoke(behaviorBuilder);

            scenarioWithEndpoint.WithComponent(new OutOfProcessEndpointBehavior(endpointName, endpointReference, behaviorBuilder.Whens));

            return scenarioWithEndpoint;
        }
    }
}