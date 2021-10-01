using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace GenericHostSnippets
{
    public class When_sending_AMessage
    {
        [Test]
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            // begin-snippet: with-generic-host-endpoint
            _ = await Scenario.Define<IntegrationScenarioContext>()
                .WithGenericHostEndpoint("endpoint-name", configPreview => Program.CreateHostBuilder(new string[0], configPreview).Build()) 
            // end-snippet
                .Done(ctx=>false)
                .Run();
        }
    }

    class Program
    {
        // begin-snippet: basic-generic-host-endpoint
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args);
            builder.UseConsoleLifetime();

            builder.UseNServiceBus(ctx =>
            {
                var config = new EndpointConfiguration("endpoint-name");
                config.UseTransport(new LearningTransport());

                return config;
            });

            return builder;
        }
        // end-snippet
        
        // begin-snippet: basic-generic-host-endpoint-with-config-previewer
        public static IHostBuilder CreateHostBuilder(string[] args, Action<EndpointConfiguration> configPreview)
        {
            var builder = Host.CreateDefaultBuilder(args);
            builder.UseConsoleLifetime();

            builder.UseNServiceBus(ctx =>
            {
                var config = new EndpointConfiguration("endpoint-name");
                config.UseTransport(new LearningTransport());

                configPreview?.Invoke(config);
                
                return config;
            });

            return builder;
        }
        // end-snippet
    }
}