using NServiceBus;
using System;
using System.Threading.Tasks;

namespace MyService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = typeof(Program).Namespace;

            var endpointConfiguration = new MyServiceConfiguration();
            var endpointInstance = await Endpoint.Start(endpointConfiguration);

            Console.WriteLine($"{typeof(Program).Namespace} started. Press any key to stop.");
            Console.ReadLine();

            await endpointInstance.Stop();
        }
    }
}
