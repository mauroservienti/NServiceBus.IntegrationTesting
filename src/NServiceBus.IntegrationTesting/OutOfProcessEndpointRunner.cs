using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.IntegrationTesting.OutOfProcess;
using NServiceBus.Logging;

namespace NServiceBus.IntegrationTesting
{
    class OutOfProcessEndpointRunner : ComponentRunner
    {
        RemoteEndpointClient remoteEndpoint;
        static ILog Logger = LogManager.GetLogger<EndpointRunner>();
        private Process process;
        private Task<string> outputTask;
        private Task<string> errorTask;
        readonly RunDescriptor runDescriptor;
        private readonly EndpointReference reference;
        readonly IList<IWhenDefinition> whens;

        public OutOfProcessEndpointRunner(RunDescriptor runDescriptor, string endpointName, Process process, IList<IWhenDefinition> whens)
        {
            remoteEndpoint = new RemoteEndpointClient();
            Name = endpointName;
            this.runDescriptor = runDescriptor;
            this.process = process;
            this.whens = whens;
        }

        public override string Name { get; }

        public override async Task Start(CancellationToken token)
        {
            var started = process.Start();

            outputTask = process.StandardOutput.ReadToEndAsync();
            errorTask = process.StandardError.ReadToEndAsync();

            EnsureEndpointIsConfiguredForTests();

            //build the remote session proxy
            IMessageSession messageSession = null;
            
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
            //TODO: ensure can communicate with remote process
        }

        public override async Task Stop()
        {
            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;
            try
            {
                await remoteEndpoint.Stop();

                var output = await outputTask;
                var error = await errorTask;

                if (output != string.Empty)
                {
                    Console.WriteLine(output);
                }

                if (error != string.Empty)
                {
                    throw new Exception(error);
                }

                process.Dispose();
                process = null;
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