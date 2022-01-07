using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    class DebuggerAttachedCallback : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(container =>
            {
                var client = container.GetService<OutOfProcessEndpointRunnerClient>();
                return new DebuggerAtachedStartupTask(client);
            });
        }
    }

    class DebuggerAtachedStartupTask : FeatureStartupTask
    {
        private OutOfProcessEndpointRunnerClient client;
        private CancellationTokenSource cancellationTokenSource;
        private Task checkDebuggerTask;

        public DebuggerAtachedStartupTask(OutOfProcessEndpointRunnerClient client)
        {
            this.client = client;
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            cancellationTokenSource = new CancellationTokenSource();

            checkDebuggerTask = Task.Run(() => CheckDebuggerStatus(cancellationTokenSource.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        protected override async Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            cancellationTokenSource.Cancel();

            if (checkDebuggerTask != null)
            {
                await checkDebuggerTask;
            }
        }

        async Task CheckDebuggerStatus(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (Debugger.IsAttached)
                    {
                        await client.SetContextProperty(Properties.DebuggerAttached, bool.TrueString);
                        break;
                    }

                    await Task.Delay(100);
                }
                catch (Exception ex) when (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
