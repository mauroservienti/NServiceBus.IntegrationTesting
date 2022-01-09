using System;
using System.IO;
using System.Linq;

namespace NServiceBus.IntegrationTesting
{
    public class EndpointReference
    {
        private string projectName;

        private EndpointReference(string projectName)
        {
            this.projectName = projectName;
        }

        public static EndpointReference FromSolutionProject(string projectName)
        {
            return new EndpointReference(projectName);
        }

        internal string GetProjectFilePath()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    var projectFilePath = Directory.EnumerateFiles(directory, $"{projectName}.csproj", SearchOption.AllDirectories).SingleOrDefault();
                    if (!File.Exists(projectFilePath))
                    {
                        throw new Exception($"Unable to find a project file that matches the supplied project name {projectName}");
                    }

                    return projectFilePath;
                }

                var parent = Directory.GetParent(directory);
                if (parent == null)
                {
                    throw new Exception($"Unable to determine the solution directory path due to the absence of a solution file.");
                }

                directory = parent.FullName;
            }
        }
    }
}