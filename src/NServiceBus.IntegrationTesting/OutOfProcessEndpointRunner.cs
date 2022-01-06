using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
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
        bool remoteEndpointStarted;
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
            
            await ConnectToRemoteEndpointWithRetries();

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

        private async Task ConnectToRemoteEndpointWithRetries()
        {
            var connected = false;
            
            //TODO: should this be configurable?
            var maxAttempts = 10;
            var attempts = 0;

            while (!connected)
            {
                try
                {
                    Logger.Debug($"Connecting to remote endpoint: '{Name}' - Attempt {attempts + 1}");
                    await remoteEndpoint.OnEndpointStarted(e =>
                    {
                        Logger.Info($"Remote endpoint '{e.EndpointName}' started.");

                        remoteEndpointStarted = true;
                        return Task.CompletedTask;
                    });

                    connected = true;
                }
                catch (Exception ex) when ((ex is RpcException rpcEx) && rpcEx.StatusCode == StatusCode.Unavailable)
                {
                    attempts++;
                    if (attempts > maxAttempts)
                    {
                        Logger.Error($"Failed to connect to remote endpoint '{Name}' after {attempts + 1} attempts.", rpcEx);
                        throw;
                    }

                    Logger.Debug($"Failed to connect to remote endpoint '{Name}', waiting to retry.");
                    //TODO: should this be configurable?
                    await Task.Delay(500);
                }
            }
        }

        public override async Task Stop()
        {
            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;

            List<Exception> errors = new();
            try
            {
                if (!remoteEndpointStarted)
                {
                    Logger.Warn($"Remote endpoint '{Name}' never published the started event. Killing the process.");
                    process.Kill();
                }
                else
                {
                    Logger.Info($"Attempt to gracefully shutdown remote endpoint '{Name}'.");
                    await remoteEndpoint.Stop();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to stop remote endpoint {Name}.", ex);
                errors.Add(ex);
            }

            var output = await outputTask;
            var error = await errorTask;

            if (output != string.Empty)
            {
                Logger.Info(output);
            }

            if (error != string.Empty)
            {
                Logger.Error(error);
                errors.Add(new Exception(error));
            }

            process.Dispose();
            process = null;

            errors.AddRange(GetFailedMessagesExceptions());

            if (errors.Any())
            {
                throw new AggregateException(errors.ToArray());
            }
        }

        IEnumerable<Exception> GetFailedMessagesExceptions()
        {
            foreach (var failedMessage in runDescriptor.ScenarioContext.FailedMessages.Where(kvp => kvp.Key == Name))
            {
                Logger.Error($"Message failed: {failedMessage.Value.First()}");
                yield return new MessageFailedException(failedMessage.Value.First(), runDescriptor.ScenarioContext);
            }
        }
    }
}