using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Logging;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class AutoConfiguration : INeedInitialization
    {
        static ILog Logger = LogManager.GetLogger<AutoConfiguration>();

        public void Customize(EndpointConfiguration builder)
        {
            if (!CommandLine.IsIntegrationTest())
            {
                Logger.Warn("This package is designed for intergration testing when " +
                    "using NServiceBus.IntegrationTesting. Do not deploy this package " +
                    "to any environment. Skipping configuration.");

                return;
            }

            var endpointName = CommandLine.GetEndpointName();

            var remoteEndpointServer = new RemoteEndpointServerV8(CommandLine.GetEndpointPort());
            remoteEndpointServer.Start();

            var testRunnerClient = new OutOfProcessEndpointRunnerClient(endpointName, CommandLine.GetRunnerPort());

            builder.RegisterComponents(services => services.AddSingleton(remoteEndpointServer));
            builder.RegisterComponents(services => services.AddSingleton(testRunnerClient));
            builder.EnableFeature<EndpointStartupCallback>();
            builder.EnableFeature<DebuggerAttachedCallback>();

            builder.Pipeline.Register(new InterceptSendOperations(endpointName, testRunnerClient), "Intercept send operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptPublishOperations(endpointName, integrationScenarioContextClient), "Intercept publish operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptReplyOperations(endpointName, integrationScenarioContextClient), "Intercept reply operations reporting them to the remote test engine.");
            //builder.Pipeline.Register(new InterceptInvokedHandlers(endpointName, integrationScenarioContextClient), "Intercept invoked Message Handlers and Sagas reporting them to the remote test engine.");
            //builder.Pipeline.Register(new RescheduleTimeoutsBehavior(integrationScenarioContextClient), "Intercept serialized message dispatch reporting them to the remote test engine.");
        }
    }
}
