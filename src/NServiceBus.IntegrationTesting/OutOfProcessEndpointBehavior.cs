using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    class OutOfProcessEndpointBehavior : IComponentBehavior
    {
        readonly EndpointReference reference;
        readonly IList<IWhenDefinition> whens;
        readonly string endpointName;

        public OutOfProcessEndpointBehavior(string endpointName, EndpointReference reference, IList<IWhenDefinition> whens)
        {
            this.endpointName = endpointName;
            this.reference = reference;
            this.whens = whens;
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

            var runner = new OutOfProcessEndpointRunner(runDescriptor, endpointName, process, whens);
            return Task.FromResult((ComponentRunner)runner);
        }
    }
}