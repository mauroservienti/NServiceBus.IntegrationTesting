using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using System;
using Serilog;

namespace MyService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, Action<EndpointConfiguration> configPreview = null)
        {
            var builder = Host.CreateDefaultBuilder(args);
            builder.UseConsoleLifetime();
            
            builder.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());

            builder.ConfigureLogging((ctx, logging) =>
            {
                logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                logging.AddConsole();
            });

            builder.UseNServiceBus(ctx =>
            {
                var config = new MyServiceConfiguration();
                configPreview?.Invoke(config);

                return config;
            });

            return builder;
        }
    }
}