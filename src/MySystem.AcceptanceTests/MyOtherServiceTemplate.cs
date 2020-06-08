using MyOtherService;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.IntegrationTesting;
using System;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    class MyOtherServiceTemplate : EndpointTemplate
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var config = MyOtherServiceConfigurationBuilder.Build(
                "MyOtherService",
                "host=localhost;username=guest;password=guest");
            return Task.FromResult(config);
        }
    }
}
