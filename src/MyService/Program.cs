using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyService;
using NServiceBus;

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

            builder.ConfigureLogging((ctx, logging) =>
            {
                logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                logging.AddConsole();
            });

            builder.UseNServiceBus(ctx => new MyServiceConfiguration());

            return builder;
        }
    }
}