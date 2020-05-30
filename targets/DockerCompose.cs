using System;
using System.IO;
using static SimpleExec.Command;

namespace Targets
{
    class DockerCompose : IDisposable
    {
        readonly string testProjDirectory;
        readonly string dockerComposeYmlFullPath;
        readonly bool useDockerCompose;

        public DockerCompose(string testProjectFullPath, string dockerComposeYml = "docker-compose.yml")
        {
            testProjDirectory = testProjectFullPath.Replace(Path.GetFileName(testProjectFullPath), "");
            dockerComposeYmlFullPath = Path.Combine(testProjDirectory, dockerComposeYml);

            useDockerCompose = File.Exists(dockerComposeYmlFullPath);
            if (useDockerCompose)
            {
                Console.WriteLine($"{Path.GetFileNameWithoutExtension(testProjectFullPath)} is configured to use docker. Setting up docker using {dockerComposeYml} file.");
                Run("docker-compose", "up -d", workingDirectory: testProjDirectory);
            }
        }

        public void Dispose()
        {
            if (useDockerCompose)
            {
                Console.WriteLine("docker-compose tear down.");
                Run("docker-compose", "down", workingDirectory: testProjDirectory);
            }
        }
    }
}
