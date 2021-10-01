using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    public class GenericHostEndpointBehavior : IComponentBehavior
    {
        readonly Func<IHost> hostBuilder;
        readonly IList<IWhenDefinition> whens;
        readonly string endpointName;

        public GenericHostEndpointBehavior(string endpointName, Func<IHost> hostBuilder, IList<IWhenDefinition> whens)
        {
            this.endpointName = endpointName;
            this.hostBuilder = hostBuilder;
            this.whens = whens;
        }

        public Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            var host = hostBuilder();
            var runner = new GenericHostEndpointRunner(run, endpointName, host, whens);
            return Task.FromResult((ComponentRunner)runner);
        }
    }
}