using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class EndpointStartupCallback : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask( container =>
            {
                var server = container.GetService<RemoteEndpointServerV8>();
                return new CallbackStartupTask(server);
            });
        }
    }

    class CallbackStartupTask : FeatureStartupTask
    {
        readonly RemoteEndpointServerV8 _server;

        public CallbackStartupTask(RemoteEndpointServerV8 server)
        {
            _server = server;
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            _server.RegisterMessageSession(session);
            _server.NotifyEndpointStarted();

            return Task.CompletedTask;
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
