using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public class ServiceTemplate<T> : IEndpointSetupTemplate
        where T : EndpointConfiguration, new()
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return OnGetConfiguration(runDescriptor, endpointConfiguration, configurationBuilderCustomization);
        }

        protected virtual Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = new T();

            var settings = configuration.GetSettings();
            endpointCustomizationConfiguration.EndpointName = settings.EndpointName();

            configurationBuilderCustomization(configuration);

            configuration.Pipeline.Register(new InterceptInvokedHandlers(endpointCustomizationConfiguration.EndpointName), "Intercept invoked Message Handlers");

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }

    public class ServiceTemplate<T, C> : ServiceTemplate<T>
        where T : EndpointConfiguration, new()
        where C : IHandleTestCompletion, new()
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            runDescriptor.OnTestCompleted(summary =>
            {
                return new C().OnTestCompleted(summary);
            });

            return base.OnGetConfiguration(runDescriptor, endpointConfiguration, configurationBuilderCustomization);
        }
    }
}
