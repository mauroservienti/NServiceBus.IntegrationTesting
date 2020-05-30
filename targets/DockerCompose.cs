using System;
using System.IO;
using System.Threading;
using static SimpleExec.Command;

namespace Targets
{
    class DockerCompose : IDisposable
    {
        readonly string testProjDirectory;
        readonly string dockerComposeYmlFullPath;
        readonly bool useDockerCompose;
        readonly string dockerComposeYml = "docker-compose.yml";

        public DockerCompose(string testProjectFullPath, Func<bool> dockerStatusChecker = null)
        {
            testProjDirectory = testProjectFullPath.Replace(Path.GetFileName(testProjectFullPath), "");
            dockerComposeYmlFullPath = Path.Combine(testProjDirectory, dockerComposeYml);

            useDockerCompose = File.Exists(dockerComposeYmlFullPath);
            if (useDockerCompose)
            {
                Console.WriteLine($"{Path.GetFileNameWithoutExtension(testProjectFullPath)} is configured to use docker. Setting up docker using {dockerComposeYml} file.");
                Run("docker-compose", "up -d", workingDirectory: testProjDirectory);
                Run("docker", "ps -a");

                if (dockerStatusChecker != null)
                {
                    while (!dockerStatusChecker())
                    {
                        Thread.Sleep(500);
                    }
                }
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
