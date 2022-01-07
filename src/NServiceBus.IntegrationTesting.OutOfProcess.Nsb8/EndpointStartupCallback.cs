using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class EndpointStartupCallback : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(container =>
            {
                var server = container.GetService<RemoteEndpointServerV8>();
                var client = container.GetService<TestRunnerClient>();
                return new CallbackStartupTask(server, client);
            });
        }
    }

    class CallbackStartupTask : FeatureStartupTask
    {
        readonly RemoteEndpointServerV8 server;
        readonly TestRunnerClient client;

        public CallbackStartupTask(RemoteEndpointServerV8 server, TestRunnerClient client)
        {
            this.server = server;
            this.client = client;
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            server.RegisterMessageSession(session);
            return client.OnEndpointStarted();
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
