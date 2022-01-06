using System;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Serilog;

namespace MyService.TestProxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MyService.Program.CreateHostBuilder(args).Build().Run();
        }
    }
}