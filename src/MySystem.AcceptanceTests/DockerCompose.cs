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
            Run("docker", "compose up -d", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
            Run("docker", "ps -a");

            static async Task<bool> statusChecker()
            {
                try
                {
                    var managementUrl = "http://localhost:15672/";
#if NETCOREAPP
                    using var client = new HttpClient();
                    var response = await client.GetAsync(managementUrl);
                    return response.IsSuccessStatusCode;
#endif

#if NET48
                    var request = HttpWebRequest.Create(managementUrl);
                    var response = await request.GetResponseAsync();
                    return ((HttpWebResponse)response).StatusCode == HttpStatusCode.OK;
#endif
                }
                catch
                {
                    return false;
                }
            }
            while (!await statusChecker())
            {
                await Task.Delay(500);
            }
        }

        public static void Down()
        {
            Run("docker", "compose down", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
