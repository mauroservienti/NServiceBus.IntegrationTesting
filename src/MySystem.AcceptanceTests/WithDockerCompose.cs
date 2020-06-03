using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading;
using static SimpleExec.Command;

namespace MySystem.AcceptanceTests
{
    public class WithDockerCompose
    {
        [SetUp]
        public void Up()
        {
            Run("docker-compose", "up -d", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
            Run("docker", "ps -a");

            static bool statusChecker()
            {
                try
                {
                    using var client = new HttpClient();
                    var response = client.GetAsync("http://localhost:15672/").GetAwaiter().GetResult();
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
            while (!statusChecker())
            {
                Thread.Sleep(500);
            }
        }

        [TearDown]
        public void Down()
        {
            Run("docker-compose", "down", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
