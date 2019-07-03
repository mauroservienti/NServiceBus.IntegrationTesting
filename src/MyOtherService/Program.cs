using NServiceBus;
using System;
using System.Threading.Tasks;

namespace MyOtherService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = typeof(Program).Namespace;

            var endpointConfiguration = new MyOtherServiceConfiguration();
            var endpointInstance = await Endpoint.Start(endpointConfiguration);

            Console.WriteLine($"{typeof(Program).Namespace} started. Press any key to stop.");
            Console.ReadLine();

            await endpointInstance.Stop();
        }
    }
}
