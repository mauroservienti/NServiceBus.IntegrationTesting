using System.IO;
using static Bullseye.Targets;
using static SimpleExec.Command;

internal class Program
{
    public static void Main(string[] args)
    {
        var sourceDir = "src";
        var dockerComposeYml = "docker-compose.yml";
        var dockerComposePath = Path.Combine("/", "src", dockerComposeYml);

        var sdk = new DotnetSdkManager();

        Target("default", DependsOn("test"));

        Target("build",
            Directory.EnumerateFiles(sourceDir, "*.sln", SearchOption.AllDirectories),
            solution => Run(sdk.GetDotnetCliPath(), $"build \"{solution}\" --configuration Release"));

        Target("test", DependsOn("build"),
            Directory.EnumerateFiles(sourceDir, "*Tests.csproj", SearchOption.AllDirectories),
            proj => 
            {
                Run("docker-compose", @$"-f {dockerComposePath} up -d");
                Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");
                Run("docker-compose", @$"-f {dockerComposePath} down");
            });

        RunTargetsAndExit(args);
    }
}
