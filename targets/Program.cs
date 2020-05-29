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
                var projFileNameWithoutExt = Path.GetFileNameWithoutExtension(proj);
                var dockerComposeYmlFullPath = proj.Replace(Path.GetFileName(proj), $"{projFileNameWithoutExt}.docker-compose.yml");
                var testProjDirectory = proj.Replace(Path.GetFileName(proj), "");
                var dockerComposeCommand = Path.Combine(testProjDirectory, "docker-compose");
                Console.WriteLine($"docker-compose command: {dockerComposeCommand}");

                var useDockerCompose = File.Exists(dockerComposeYmlFullPath);
                if (useDockerCompose) 
                {
                    Console.WriteLine($"{projFileNameWithoutExt} is configured to use docker. Setting up docker using docker-compose ({dockerComposeYmlFullPath})");
                    Run(dockerComposeCommand, $"--file \"{dockerComposeYmlFullPath}\" up");
                }

                Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");

                if (useDockerCompose)
                {
                    Console.WriteLine("docker-compose tear down.");
                    Run(dockerComposeCommand, $"--file \"{dockerComposeYmlFullPath}\" down");
                }
            });

        RunTargetsAndExit(args);
    }
}
