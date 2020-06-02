using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public abstract class EndpointTemplate : IEndpointSetupTemplate
    {
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = await OnGetConfiguration(runDescriptor, endpointCustomizationConfiguration, configurationBuilderCustomization);

            var settings = configuration.GetSettings();
            endpointCustomizationConfiguration.EndpointName = settings.EndpointName();

            configurationBuilderCustomization(configuration);

            configuration.Pipeline.Register(new InterceptInvokedHandlers(endpointCustomizationConfiguration.EndpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext), "Intercept invoked Message Handlers and Sagas");
            configuration.Pipeline.Register(new InterceptSendOperations(endpointCustomizationConfiguration.EndpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext), "Intercept send operations");
            configuration.Pipeline.Register(new InterceptPublishOperations(endpointCustomizationConfiguration.EndpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext), "Intercept publish operations");
            configuration.Pipeline.Register(new InterceptReplyOperations(endpointCustomizationConfiguration.EndpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext), "Intercept reply operations");

            return configuration;
        }

        protected abstract Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization);
    }

    public class EndpointTemplate<T> : EndpointTemplate
        where T : EndpointConfiguration, new()
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var configuration = new T();

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }

    public class EndpointTemplate<T, C> : EndpointTemplate<T>
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
