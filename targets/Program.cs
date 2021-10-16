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
        var isUnix = Environment.OSVersion.Platform == PlatformID.Unix;
        var unixFx = "netcoreapp3.1";

        Target("default", DependsOn("test"));

        Target("build",
            Directory.EnumerateFiles(sourceDir, "*.sln", SearchOption.AllDirectories),
            solution => Run(sdk.GetDotnetCliPath(), $"build \"{solution}\" --configuration Release"));

        Target("test", DependsOn("build"),
            Directory.EnumerateFiles(sourceDir, "*Tests.csproj", SearchOption.AllDirectories),
            proj => Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" {(isUnix ? "--framework " + unixFx : "")} --configuration Release --no-build"));

        RunTargetsAndExit(args);
    }
}
