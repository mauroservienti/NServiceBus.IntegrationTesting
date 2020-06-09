using System;
using System.IO;
using System.Linq;

namespace NServiceBus
{
    public static class IncludeOnlyExtension
    {
        // begin-snippet: include-only-extension
        public static AssemblyScannerConfiguration IncludeOnly(this AssemblyScannerConfiguration configuration, params string[] assembliesToInclude)
        {
            var excluded = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
                .Select(path => Path.GetFileName(path))
                .Where(existingAssembly => !assembliesToInclude.Contains(existingAssembly))
                .ToArray();

            configuration.ExcludeAssemblies(excluded);

            return configuration;
        }
        // end-snippet
    }
}
