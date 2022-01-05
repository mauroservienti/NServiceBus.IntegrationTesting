using Grpc.Core;
using NServiceBus.IntegrationTesting.OutOfProcess;

namespace NServiceBus
{
    public static class EndpointConfigurationExtensions
    {
        public static void EnableOutOfProcessIntegrationTesting(this EndpointConfiguration builder, string endpointName)
        {
            _ = new RemoteEndpointServer();

            var integrationScenarioContextClient = new IntegrationScenarioContextClient();

            builder.Pipeline.Register(new InterceptSendOperations(endpointName, integrationScenarioContextClient), "Intercept send operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptPublishOperations(endpointName, integrationScenarioContextClient), "Intercept publish operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptReplyOperations(endpointName, integrationScenarioContextClient), "Intercept reply operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptInvokedHandlers(endpointName, integrationScenarioContextClient), "Intercept invoked Message Handlers and Sagas reporting them to the remote test engine.");
            //builder.Pipeline.Register(new RescheduleTimeoutsBehavior(integrationScenarioContextClient), "Intercept serialized message dispatch reporting them to the remote test engine.");
        }
    }
}
