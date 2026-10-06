using System.Runtime.CompilerServices;

namespace NServiceBus.IntegrationTesting.Tests;

static class Approver
{
    public static void Verify(string value, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
    {
        var directory = Path.GetDirectoryName(callerFilePath)!;
        var baseName = $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMemberName}";
        var approvedFile = Path.Combine(directory, $"{baseName}.approved.txt");
        var receivedFile = Path.Combine(directory, $"{baseName}.received.txt");

        var received = Normalize(value);
        var approved = File.Exists(approvedFile) ? Normalize(File.ReadAllText(approvedFile)) : null;

        if (received == approved)
        {
            File.Delete(receivedFile);
            return;
        }

        File.WriteAllText(receivedFile, received);
        Assert.Fail($"Received value does not match '{approvedFile}'. If the change is expected, replace the approved file with '{receivedFile}'.");
    }

    static string Normalize(string value) => value.Replace("\r\n", "\n").TrimEnd();
}
