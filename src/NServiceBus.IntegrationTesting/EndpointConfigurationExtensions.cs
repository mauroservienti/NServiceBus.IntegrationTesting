using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting;

namespace NServiceBus.IntegrationTesting
{
    public static class EndpointConfigurationExtensions
    {
        public static void RegisterRequiredPipelineBehaviors(this EndpointConfiguration builder, string endpointName, IntegrationScenarioContext integrationScenarioContext)
        {
            builder.Pipeline.Register(new InterceptInvokedHandlers(endpointName, integrationScenarioContext), "Intercept invoked Message Handlers and Sagas");
            builder.Pipeline.Register(new InterceptSendOperations(endpointName, integrationScenarioContext), "Intercept send operations");
            builder.Pipeline.Register(new InterceptPublishOperations(endpointName, integrationScenarioContext), "Intercept publish operations");
            builder.Pipeline.Register(new InterceptReplyOperations(endpointName, integrationScenarioContext), "Intercept reply operations");
            builder.Pipeline.Register(new RescheduleTimeoutsBehavior(integrationScenarioContext), "Intercept serialized message dispatch");
        }

        public static void RegisterScenarioContext(this EndpointConfiguration builder, ScenarioContext scenarioContext)
        {
            builder.RegisterComponents(r => { RegisterInheritanceHierarchyOfContextOnContainer(scenarioContext, r); });
        }

        static void RegisterInheritanceHierarchyOfContextOnContainer(ScenarioContext scenarioContext, IServiceCollection r)
        {
            var type = scenarioContext.GetType();
            while (type != typeof(object))
            {
                if (type == null)
                {
                    continue;
                }

                r.AddSingleton(type, scenarioContext);
                type = type.BaseType;
            }
        }
    }
}