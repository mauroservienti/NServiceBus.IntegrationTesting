using System;
using System.Threading.Tasks;
using static SimpleExec.Command;

#if NETCOREAPP
using System.Net.Http;
#endif

#if NET48
using System.Net;
#endif

namespace MySystem.AcceptanceTests
{
    public static class DockerCompose
    {
        public static async Task Up()
        {
            await RunAsync("docker", "compose up -d", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
            await RunAsync("docker", "ps -a");

            while (!await StatusChecker())
            {
                await Task.Delay(500);
            }

            return;

            static async Task<bool> StatusChecker()
            {
                try
                {
                    using var client = new HttpClient();
                    var response = await client.GetAsync("http://localhost:15672/");
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Down()
        {
            Run("docker", "compose down", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
