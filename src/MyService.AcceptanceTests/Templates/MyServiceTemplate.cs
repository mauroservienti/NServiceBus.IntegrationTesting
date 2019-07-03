using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using System;
using System.Threading.Tasks;

namespace MyService.AcceptanceTests.Templates
{
    class ServiceTemplate<T> : IEndpointSetupTemplate where T : EndpointConfiguration, new()
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = new T();
            configuration.EnableInstallers();

            configurationBuilderCustomization(configuration);

            //need the cleanup stuff

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }
}
