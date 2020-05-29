using System.Diagnostics;
using System.IO;
using System.Linq;
using static Bullseye.Targets;
using static SimpleExec.Command;

internal class Program
{
    public static void Main(string[] args)
    {
        var sourceDir = "src";
        var dockerCompose = Directory.EnumerateFiles(sourceDir, "docker-compose.yml", SearchOption.AllDirectories).Single();
        var dockerComposeFullPath = Path.GetFullPath(dockerCompose);
        Debug.Assert(File.Exists(dockerCompose));
        Debug.Assert(File.Exists(dockerComposeFullPath));

        var sdk = new DotnetSdkManager();

        Target("default", DependsOn("test"));

        Target("build",
            Directory.EnumerateFiles(sourceDir, "*.sln", SearchOption.AllDirectories),
            solution => Run(sdk.GetDotnetCliPath(), $"build \"{solution}\" --configuration Release"));

        Target("test", DependsOn("build"),
            Directory.EnumerateFiles(sourceDir, "*Tests.csproj", SearchOption.AllDirectories),
            proj => 
            {
                Run("docker-compose", @$"-f {dockerComposeFullPath} up -d");
                Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");
                Run("docker-compose", @$"-f {dockerComposeFullPath} down");
            });

        RunTargetsAndExit(args);
    }
}
