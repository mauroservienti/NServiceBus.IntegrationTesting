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
        readonly IList<IRemoteEndpointWhenDefinition> remoteWhens;

        public OutOfProcessEndpointRunner(RunDescriptor runDescriptor, string endpointName, Process process, IList<IRemoteEndpointWhenDefinition> remoteWhens)
        {
            remoteEndpoint = new RemoteEndpointClient();
            Name = endpointName;
            this.runDescriptor = runDescriptor;
            this.process = process;
            this.remoteWhens = remoteWhens;
        }

        public override string Name { get; }

        public override async Task Start(CancellationToken token)
        {
            var started = process.Start();

            outputTask = process.StandardOutput.ReadToEndAsync();
            errorTask = process.StandardError.ReadToEndAsync();
            
            await ConnectToRemoteEndpointWithRetries();

            while (!remoteEndpointStarted)
            {
                Logger.Debug($"Waiting for remote endpoint '{Name}' to start.");
                await Task.Delay(100);
            }

            Logger.Info($"Remote endpoint '{Name}' started. Executing whens.");

            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;

            //build the remote session proxy
            IRemoteMessageSessionProxy messageSessionProxy = null;

            try
            {
                if (remoteWhens.Count != 0)
                {
                    await Task.Run(async () =>
                    {
                        var executedWhens = new HashSet<Guid>();

                        while (!token.IsCancellationRequested)
                        {
                            if (executedWhens.Count == remoteWhens.Count)
                            {
                                break;
                            }

                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            foreach (var when in remoteWhens)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    break;
                                }

                                if (executedWhens.Contains(when.Id))
                                {
                                    continue;
                                }

                                if (await when.ExecuteAction((IntegrationScenarioContext)runDescriptor.ScenarioContext, messageSessionProxy).ConfigureAwait(false))
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
                Logger.Error($"Failed to execute Whens on endpoint {Name}", ex);

                throw;
            }
        }

        private async Task ConnectToRemoteEndpointWithRetries()
        {
            var connected = false;
            
            //NServiceBus ATT comes with a default hardcoded 2 minutes endpoints startup timeout
            var maxAttempts = 240;
            var msDelayBetweenAttempts = 500;
            var attempts = 0;

            while (!connected)
            {
                try
                {
                    Logger.Debug($"Connecting to remote endpoint: '{Name}' - Attempt {attempts + 1}");
                    await remoteEndpoint.OnEndpointStarted(e =>
                    {
                        Logger.Info($"Received EndpointStarted event from remote endpoint '{e.EndpointName}'.");

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
                    await Task.Delay(msDelayBetweenAttempts);
                }
            }

            Logger.Debug($"Connected to remote endpoint: '{Name}'.");
        }

        public override async Task Stop()
        {
            Logger.Debug($"OutOfProcess runner for remote endpoint '{Name}' stop requested.");
            //TODO: How to access ScenarioContext.CurrentEndpoint
            // ScenarioContext.CurrentEndpoint = Name;

            List<Exception> errors = new();
            try
            {
                if (!remoteEndpointStarted)
                {
                    Logger.Warn($"Remote endpoint '{Name}' never published the started event. Killing the process.");
                    process.Kill();
                    Logger.Warn($"Remote endpoint '{Name}' killed.");
                }
                else
                {
                    Logger.Info($"Attempt to gracefully shutdown remote endpoint '{Name}'.");
                    await remoteEndpoint.Stop();
                    Logger.Info($"Remote endpoint '{Name}' gracefully shutdown.");
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

            Logger.Debug($"OutOfProcess runner for remote endpoint '{Name}' stop completed.");
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