﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using static SimpleExec.Command;

namespace MySystem.AcceptanceTests
{
    public static class DockerCompose
    {
        public static async Task Up()
        {
            Run("docker-compose", "up -d", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
            Run("docker", "ps -a");

            static async Task<bool> statusChecker()
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
            while (!await statusChecker())
            {
                await Task.Delay(500);
            }
        }

        public static void Down()
        {
            Run("docker-compose", "down", workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
