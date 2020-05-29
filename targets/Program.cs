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
        //var dockerComposeYmlFullPath = Path.GetFullPath(Directory.EnumerateFiles(sourceDir, "docker-compose.yml", SearchOption.AllDirectories).Single());

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
                var projFileName = Path.GetFileName(proj);
                var dockerComposeYmlFileName = $"{projFileNameWithoutExt}.docker-compose.yml";
                var dockerComposeYmlFullPath = proj.Replace(projFileName, dockerComposeYmlFileName);


                Run("docker-compose", @$"up -f \"{dockerComposeYmlFullPath}\" -d");
                Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build");
                Run("docker-compose", @$"down -f \"{dockerComposeYmlFullPath}\"");
            });

        RunTargetsAndExit(args);
    }
}
