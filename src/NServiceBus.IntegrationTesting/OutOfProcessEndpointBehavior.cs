using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Logging;

namespace NServiceBus.IntegrationTesting
{
    class OutOfProcessEndpointBehavior : IComponentBehavior
    {
        static ILog Logger = LogManager.GetLogger<OutOfProcessEndpointBehavior>();

        readonly EndpointReference reference;
        readonly IList<IRemoteEndpointWhenDefinition> remoteWhens;
        readonly string endpointName;

        public OutOfProcessEndpointBehavior(string endpointName, EndpointReference reference, IList<IRemoteEndpointWhenDefinition> remoteWhens)
        {
            this.endpointName = endpointName;
            this.reference = reference;
            this.remoteWhens = remoteWhens;
        }

        public Task<ComponentRunner> CreateRunner(RunDescriptor runDescriptor)
        {
            (var runnerPort, var endpointPort) = ((IntegrationScenarioContext)runDescriptor.ScenarioContext).GetCommunicationPorts(endpointName);
            
            var process = new Process();
            process.StartInfo.FileName = @"dotnet";
            process.StartInfo.Arguments = $"run --project \"{reference.GetProjectFilePath()}\" --integrationTest --endpointName={endpointName} --runnerPort={runnerPort} --endpointPort={endpointPort}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;

            Logger.Debug($"Remote endpoint '{endpointName}' process arguments: {process.StartInfo.Arguments}");

            var runner = new OutOfProcessEndpointRunner(runDescriptor, endpointName, process, remoteWhens, runnerPort, endpointPort);
            return Task.FromResult((ComponentRunner)runner);
        }
    }
}