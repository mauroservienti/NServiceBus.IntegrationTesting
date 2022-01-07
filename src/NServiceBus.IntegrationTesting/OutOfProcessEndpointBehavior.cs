using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    class OutOfProcessEndpointBehavior : IComponentBehavior
    {
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
            var process = new Process();
            process.StartInfo.FileName = @"dotnet";
            process.StartInfo.Arguments = $"run --project \"{reference.GetProjectFilePath()}\" --integrationTest --endpointName={endpointName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;

            var runner = new OutOfProcessEndpointRunner(runDescriptor, endpointName, process, remoteWhens);
            return Task.FromResult((ComponentRunner)runner);
        }
    }
}