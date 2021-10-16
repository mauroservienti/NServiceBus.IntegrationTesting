using System.Runtime.CompilerServices;
using ApprovalTests;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using NUnit.Framework;
using PublicApiGenerator;

namespace NServiceBus.IntegrationTesting.Tests.API
{
    public class APIApprovals
    {
        [Test]
        [UseReporter(typeof(DiffReporter))]
        [MethodImpl(MethodImplOptions.NoInlining)]
#if NETCOREAPP
        [UseApprovalSubdirectory("NETCOREAPP")]
#endif
#if NET48
        [UseApprovalSubdirectory("NET48")]
#endif
        public void Approve_API()
        {
            var publicApi = typeof(IntegrationScenarioContext).Assembly.GeneratePublicApi();
            Approvals.Verify(publicApi);
        }
    }
}