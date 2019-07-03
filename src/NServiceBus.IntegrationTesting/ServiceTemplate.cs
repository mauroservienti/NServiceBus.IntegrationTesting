using NServiceBus.AcceptanceTesting.Support;
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

        protected virtual Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = new T();

            configurationBuilderCustomization(configuration);

            configuration.Pipeline.Register(typeof(InterceptInvokedHandlers), "Intercept invoked Message Handlers");

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
