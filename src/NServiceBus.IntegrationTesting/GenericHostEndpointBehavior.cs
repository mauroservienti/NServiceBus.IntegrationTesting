using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.IntegrationTesting
{
    public class GenericHostEndpointBehavior : IComponentBehavior
    {
        readonly Func<Action<EndpointConfiguration>, IHost> hostBuilder;
        readonly IList<IWhenDefinition> whens;
        readonly string endpointName;

        public GenericHostEndpointBehavior(string endpointName, Func<Action<EndpointConfiguration>, IHost> hostBuilder, IList<IWhenDefinition> whens)
        {
            this.endpointName = endpointName;
            this.hostBuilder = hostBuilder;
            this.whens = whens;
        }

        public Task<ComponentRunner> CreateRunner(RunDescriptor runDescriptor)
        {
            var host = hostBuilder(configuration => 
            {
                configuration.RegisterRequiredPipelineBehaviors(endpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext);
                configuration.RegisterScenarioContext(runDescriptor.ScenarioContext);
            });
            var runner = new GenericHostEndpointRunner(runDescriptor, endpointName, host, whens);
            return Task.FromResult((ComponentRunner)runner);
        }
    }
}