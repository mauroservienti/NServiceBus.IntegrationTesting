using System.Runtime.CompilerServices;
using ApprovalTests;
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
        public void Approve_API()
        {
            var publicApi = typeof(IntegrationScenarioContext).Assembly.GeneratePublicApi();
            Approvals.Verify(publicApi);
        }
    }
}