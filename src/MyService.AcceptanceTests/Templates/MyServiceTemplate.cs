using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Pipeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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

            configuration.Pipeline.Register(typeof(InterceptInvokedHandlers), "Intercept invoked Message Handlers");

            //need the cleanup stuff

            return Task.FromResult<EndpointConfiguration>(configuration);
        }
    }

    class InterceptInvokedHandlers : Behavior<IInvokeHandlerContext>
    {
        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            await next();

            SpecialContext.CurrentContext.InvokedHandlers.Add(context.MessageHandler.HandlerType);
        }
    }

    abstract class SpecialContext : ScenarioContext
    {
        public List<Type> InvokedHandlers = new List<Type>();

        static PropertyInfo GetCurrentProperty()
        {
            return typeof(ScenarioContext).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static SpecialContext CurrentContext
        {
            get
            {
                var pi = GetCurrentProperty();
                var current = (SpecialContext)pi.GetMethod.Invoke(null, null);

                return current;
            }
        }
    }
}
