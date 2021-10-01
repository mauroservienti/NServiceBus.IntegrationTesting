using Microsoft.Extensions.Hosting;

namespace NServiceBus.IntegrationTesting.Tests.TestEndpoint
{
    public class Program
    {
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
                var config = new EndpointConfiguration("NServiceBus.IntegrationTesting.Tests.TestEndpoint");
                config.UseTransport<LearningTransport>();

                config.AssemblyScanner().ExcludeAssemblies("NServiceBus.IntegrationTesting.Tests.dll");

                return config;
            });

            return builder;
        }
    }
}