using NServiceBus.AcceptanceTesting.Support;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public class ServiceTemplate<T, C> : IEndpointSetupTemplate 
        where T : EndpointConfiguration, new()
        where C : IHandleTestCompletion, new()
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = new T();

            configurationBuilderCustomization(configuration);

            configuration.Pipeline.Register(typeof(InterceptInvokedHandlers), "Intercept invoked Message Handlers");

            runDescriptor.OnTestCompleted(summary =>
            {
                return new C().OnTestCompleted(summary);
            });

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }
}
