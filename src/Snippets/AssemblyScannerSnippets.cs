using NServiceBus;

namespace AssemblyScannerSnippets
{
    // begin-snippet: assembly-scanner-config
    public class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            var scanner = this.AssemblyScanner();
            scanner.IncludeOnly("MyService.dll", "MyMessages.dll");

            //rest of the configuration
        }
    }
    // end-snippet
}