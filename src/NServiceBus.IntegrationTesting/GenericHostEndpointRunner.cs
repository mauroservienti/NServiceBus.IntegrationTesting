using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Logging;

namespace NServiceBus.IntegrationTesting
{
    class GenericHostEndpointRunner : ComponentRunner
    {
        static ILog Logger = LogManager.GetLogger<EndpointRunner>();
        readonly RunDescriptor runDescriptor;
        readonly IHost host;
        readonly IList<IWhenDefinition> whens;

        public GenericHostEndpointRunner(RunDescriptor runDescriptor, string endpointName, IHost host, IList<IWhenDefinition> whens)
        {
            Name = endpointName;
            this.runDescriptor = runDescriptor;
            this.host = host;
            this.whens = whens;
        }

        public override string Name { get; }

        public override async Task Start(CancellationToken token)
        {
            await host.StartAsync(token);

            EnsureEndpointIsConfiguredForTests();

            var messageSession = host.Services.GetRequiredService<IMessageSession>();

            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;
            try
            {
                if (whens.Count != 0)
                {
                    await Task.Run(async () =>
                    {
                        var executedWhens = new HashSet<Guid>();

                        while (!token.IsCancellationRequested)
                        {
                            if (executedWhens.Count == whens.Count)
                            {
                                break;
                            }

                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            foreach (var when in whens)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    break;
                                }

                                if (executedWhens.Contains(when.Id))
                                {
                                    continue;
                                }

                                if (await when.ExecuteAction(runDescriptor.ScenarioContext, messageSession).ConfigureAwait(false))
                                {
                                    executedWhens.Add(when.Id);
                                }
                            }

                            await Task.Yield(); // enforce yield current context, tight loop could introduce starvation
                        }
                    }, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute Whens on endpoint{Name}", ex);

                throw;
            }
        }

        private void EnsureEndpointIsConfiguredForTests()
        {
            var check = host.Services.GetService<EnsureEndpointIsConfiguredForTests>();
            if (check == null)
            {
                throw new Exception($"Endpoint {Name} is not correctly configured to be tested. " +
                                    $"Make sure to pass the EndpointConfiguration instance to the " +
                                    $"Action<EndpointConfiguration> provided by WithGenericHostEndpoint " +
                                    $"tests setup method.");
            }
        }

        public override async Task Stop()
        {
            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;
            try
            {
                await host.StopAsync();
                host.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop endpoint " + Name, ex);
                throw;
            }

            ThrowOnFailedMessages();
        }

        void ThrowOnFailedMessages()
        {
            foreach (var failedMessage in runDescriptor.ScenarioContext.FailedMessages.Where(kvp => kvp.Key == Name))
            {
                throw new MessageFailedException(failedMessage.Value.First(), runDescriptor.ScenarioContext);
            }
        }
    }
}