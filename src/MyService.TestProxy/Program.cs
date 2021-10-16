﻿using System;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using Serilog;

namespace MyService.TestProxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = MyService.Program.CreateHostBuilder(args, endpointConfiguration => 
            {
                endpointConfiguration.EnableOutOfProcessIntegrationTesting("MyService");
            });

            return builder;
        }
    }
}