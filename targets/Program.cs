using System;
using System.IO;
using System.Net.Http;
using Targets;
using static Bullseye.Targets;
using static SimpleExec.Command;

internal class Program
{
    public static void Main(string[] args)
    {
        var sourceDir = "src";

        var sdk = new DotnetSdkManager();

        Target("default", DependsOn("test"));

        Target("build",
            Directory.EnumerateFiles(sourceDir, "*.sln", SearchOption.AllDirectories),
            solution => Run(sdk.GetDotnetCliPath(), $"build \"{solution}\" --configuration Release"));

        Target("test", DependsOn("build"),
            Directory.EnumerateFiles(sourceDir, "*Tests.csproj", SearchOption.AllDirectories),
            proj => 
            {
                static bool statusChecker()
                {
                    using var client = new HttpClient();
                    var response = client.GetAsync("http://localhost:15672/").GetAwaiter().GetResult();
                    return response.IsSuccessStatusCode;
                }

                using (new DockerCompose(proj, statusChecker)) 
                {
                    Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");
                }                
            });

        RunTargetsAndExit(args);
    }
}
