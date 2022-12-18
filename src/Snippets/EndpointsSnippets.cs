using System;
using System.Threading.Tasks;
using MyOtherService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.IntegrationTesting;

namespace EndpointsSnippets
{
    // begin-snippet: endpoints-used-in-each-test
    public class When_sending_AMessage
    {
        class MyServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyServiceEndpoint()
            {
                EndpointSetup<EndpointTemplate<MyServiceConfiguration>>();
            }
        }

        class MyOtherServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherServiceEndpoint()
            {
                EndpointSetup<MyOtherServiceTemplate>();
            }
        }
    }
    // end-snippet

    class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration() :base(""){}
    }

    // begin-snippet: my-other-service-template
    class MyOtherServiceTemplate : EndpointTemplate
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var config = MyOtherServiceConfigurationBuilder.Build(
                "MyOtherService",
                "host=localhost;username=guest;password=guest");
            return Task.FromResult(config);
        }
    }
    // end-snippet
}