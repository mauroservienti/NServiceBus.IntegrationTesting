using Microsoft.Extensions.DependencyInjection;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class AutoConfiguration : INeedInitialization
    {
        public void Customize(EndpointConfiguration builder)
        {
            CommandLine.EnsureIsIntegrationTest();

            var endpointName = CommandLine.GetEndpointName();

            var remoteEndpointServer = new RemoteEndpointServerV8(CommandLine.GetEndpointPort());
            remoteEndpointServer.Start();

            var testRunnerClient = new OutOfProcessEndpointRunnerClient(endpointName, CommandLine.GetRunnerPort());

            builder.RegisterComponents(services => services.AddSingleton(remoteEndpointServer));
            builder.RegisterComponents(services => services.AddSingleton(testRunnerClient));
            builder.EnableFeature<EndpointStartupCallback>();
            builder.EnableFeature<DebuggerAttachedCallback>();

            builder.Pipeline.Register(new InterceptSendOperations(endpointName, testRunnerClient), "Intercept send operations streaming them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptPublishOperations(endpointName, integrationScenarioContextClient), "Intercept publish operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptReplyOperations(endpointName, integrationScenarioContextClient), "Intercept reply operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptInvokedHandlers(endpointName, integrationScenarioContextClient), "Intercept invoked Message Handlers and Sagas reporting them to the remote test engine.");
            //builder.Pipeline.Register(new RescheduleTimeoutsBehavior(integrationScenarioContextClient), "Intercept serialized message dispatch reporting them to the remote test engine.");
        }
    }
}
