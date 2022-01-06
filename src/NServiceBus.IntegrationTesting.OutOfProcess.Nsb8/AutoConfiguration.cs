using Microsoft.Extensions.DependencyInjection;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class AutoConfiguration : INeedInitialization
    {
        public void Customize(EndpointConfiguration builder)
        {
            CommandLine.EnsureIsIntegrationTest();

            var endpointName = CommandLine.GetEndpointName();

            var remoteEndpointServer = new RemoteEndpointServerV8(endpointName);

            builder.RegisterComponents(services => services.AddSingleton(remoteEndpointServer));

            builder.EnableFeature<EndpointStartupCallback>();

            var integrationScenarioContextClient = new IntegrationScenarioContextClient();

            builder.Pipeline.Register(new InterceptSendOperations(endpointName, integrationScenarioContextClient), "Intercept send operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptPublishOperations(endpointName, integrationScenarioContextClient), "Intercept publish operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptReplyOperations(endpointName, integrationScenarioContextClient), "Intercept reply operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptInvokedHandlers(endpointName, integrationScenarioContextClient), "Intercept invoked Message Handlers and Sagas reporting them to the remote test engine.");
            //builder.Pipeline.Register(new RescheduleTimeoutsBehavior(integrationScenarioContextClient), "Intercept serialized message dispatch reporting them to the remote test engine.");
        }
    }
}
