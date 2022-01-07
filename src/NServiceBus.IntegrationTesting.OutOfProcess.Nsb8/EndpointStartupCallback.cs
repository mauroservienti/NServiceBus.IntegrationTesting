using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using System;
using System.Diagnostics;
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
                var client = container.GetService<OutOfProcessEndpointRunnerClient>();
                return new CallbackStartupTask(server, client);
            });
        }
    }

    class CallbackStartupTask : FeatureStartupTask
    {
        readonly RemoteEndpointServerV8 server;
        readonly OutOfProcessEndpointRunnerClient client;

        public CallbackStartupTask(RemoteEndpointServerV8 server, OutOfProcessEndpointRunnerClient client)
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
