using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public abstract class EndpointTemplate : IEndpointSetupTemplate
    {
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var configuration = await OnGetConfiguration(runDescriptor, endpointCustomizationConfiguration, configurationBuilderCustomization);

            var settings = configuration.GetSettings();
            endpointCustomizationConfiguration.EndpointName = settings.EndpointName();

            configuration.RegisterRequiredPipelineBehaviors(endpointCustomizationConfiguration.EndpointName, (IntegrationScenarioContext)runDescriptor.ScenarioContext);
            configuration.RegisterScenarioContext(runDescriptor.ScenarioContext);

            await configurationBuilderCustomization(configuration);

            return configuration;
        }

        protected abstract Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization);
    }

    public class EndpointTemplate<T> : EndpointTemplate
        where T : EndpointConfiguration, new()
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var configuration = new T();

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }

    public class EndpointTemplate<T, C> : EndpointTemplate<T>
        where T : EndpointConfiguration, new()
        where C : IHandleTestCompletion, new()
    {
        protected override Task<EndpointConfiguration> OnGetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            runDescriptor.OnTestCompleted(summary =>
            {
                return new C().OnTestCompleted(summary);
            });

            return base.OnGetConfiguration(runDescriptor, endpointConfiguration, configurationBuilderCustomization);
        }
    }
}
