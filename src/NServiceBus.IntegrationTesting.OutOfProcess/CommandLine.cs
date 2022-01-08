using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public static class CommandLine
    {
        public static bool IsIntegrationTest()
        {
            var integrationTest = Environment.CommandLine
                .Split(' ')
                .Where(x => x == "--integrationTest")
                .SingleOrDefault();

            return integrationTest != null;
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
