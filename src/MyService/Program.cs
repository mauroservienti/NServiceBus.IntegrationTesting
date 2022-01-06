using System;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Serilog;

namespace MyService
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
            
            builder.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());

            builder.UseNServiceBus(ctx =>
            {
                var config = new MyServiceConfiguration();
                return config;
            });

            return builder;
        }
    }
}