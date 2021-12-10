using System;
using Microsoft.Data.SqlClient;
using MyMessages.Messages;
using NServiceBus;

namespace MyService
{
    class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            var connectionString = "Data Source=.;Initial Catalog=db;User ID=sa;Password=YourStrong@Passw0rd;Max Pool Size=80";
            
            var scanner = this.AssemblyScanner();
            scanner.IncludeOnly("MyService.dll", 
                "MyMessages.dll",
                "NServiceBus.Transport.SqlServer.dll",
                "NServiceBus.Persistence.Sql.dll");

            this.UseSerialization<NewtonsoftSerializer>();
            this.EnableInstallers();
            
            this.EnableOutbox();
            
            var persistence = this.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>();
            persistence.ConnectionBuilder(() => new SqlConnection(connectionString));

            var transportConfig = this.UseTransport<SqlServerTransport>()
                .ConnectionString(connectionString);
            transportConfig.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            transportConfig.Routing()
                .RouteToEndpoint(typeof(AMessage), "MyOtherService");

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
