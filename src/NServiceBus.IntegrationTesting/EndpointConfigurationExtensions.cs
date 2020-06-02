using NServiceBus.AcceptanceTesting;
using NServiceBus.ObjectBuilder;

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
        }
    }
}
