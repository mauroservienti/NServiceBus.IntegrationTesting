using Microsoft.Data.SqlClient;
using NServiceBus;

namespace MyOtherService
{
    public static class MyOtherServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName)
        {
            var connectionString = "Data Source=.;Initial Catalog=db;User ID=sa;Password=YourStrong@Passw0rd;Max Pool Size=80";
            
            var config = new EndpointConfiguration(endpointName);
            var scanner = config.AssemblyScanner();
            scanner.IncludeOnly("MyOtherService.dll", 
                "MyMessages.dll",
                "NServiceBus.Transport.SqlServer.dll",
                "NServiceBus.Persistence.Sql.dll");

            config.UseSerialization<NewtonsoftSerializer>();
            config.EnableInstallers();

            config.EnableOutbox();
            
            var persistence = config.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>();
            persistence.ConnectionBuilder(() => new SqlConnection(connectionString));

            var transportConfig = config.UseTransport<SqlServerTransport>()
                .ConnectionString(connectionString);
            transportConfig.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

            config.SendFailedMessagesTo("error");

            config.Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
            
            return config;
        }
    }
}
