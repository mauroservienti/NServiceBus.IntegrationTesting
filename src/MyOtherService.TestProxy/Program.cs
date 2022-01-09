using System;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Serilog;

namespace MyOtherService.TestProxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MyOtherService.Program.CreateHostBuilder(args).Build().Run();
        }
    }
}