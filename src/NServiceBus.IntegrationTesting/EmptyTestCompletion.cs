using NServiceBus.AcceptanceTesting.Support;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public class EmptyTestCompletionHandler : IHandleTestCompletion
    {
        public Task OnTestCompleted(RunSummary summary)
        {
            //NOP
            return Task.CompletedTask;
        }
    }
}
