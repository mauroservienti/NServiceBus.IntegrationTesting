using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public static class CommandLine
    {
        public static void EnsureIsIntegrationTest()
        {
            var integrationTest = Environment.CommandLine
                .Split(' ')
                .Where(x => x == "--integrationTest")
                .SingleOrDefault();

            if (string.IsNullOrWhiteSpace(integrationTest))
            {
                throw new ArgumentException("This library is designed for integration tests usage. If this is not an integration test remove the library dependency.");
            }
        }

        public static string GetEndpointName()
        {
            var endpointName = Environment.CommandLine
                .Split(' ')
                .Where(x => x.StartsWith("--endpointName="))
                .Single()
                .Split('=')
                .Last();

            return endpointName;
        }

        public static int GetRunnerPort()
        {
            var runnerPort = Environment.CommandLine
                .Split(' ')
                .Where(x => x.StartsWith("--runnerPort="))
                .Single()
                .Split('=')
                .Last();

            return int.Parse(runnerPort);
        }

        public static int GetEndpointPort()
        {
            var endpointPort = Environment.CommandLine
                .Split(' ')
                .Where(x => x.StartsWith("--endpointPort="))
                .Single()
                .Split('=')
                .Last();

            return int.Parse(endpointPort);
        }
    }
}
