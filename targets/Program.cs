using System;
using System.IO;
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
                var testProjDirectory = proj.Replace(Path.GetFileName(proj), "");
                var dockerComposeYml = "docker-compose.yml";
                var dockerComposeYmlFullPath = Path.Combine(testProjDirectory, dockerComposeYml);
                

                var useDockerCompose = File.Exists(dockerComposeYmlFullPath);
                if (useDockerCompose) 
                {
                    Console.WriteLine($"{Path.GetFileNameWithoutExtension(proj)} is configured to use docker. Setting up docker using {dockerComposeYml} file.");
                    Run("docker-compose", "up -d", workingDirectory: testProjDirectory);
                }

                Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");

                if (useDockerCompose)
                {
                    Console.WriteLine("docker-compose tear down.");
                    Run("docker-compose", "down", workingDirectory: testProjDirectory);
                }
            });

        RunTargetsAndExit(args);
    }
}
