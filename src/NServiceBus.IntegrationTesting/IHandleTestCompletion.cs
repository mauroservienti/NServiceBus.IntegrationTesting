using NServiceBus.AcceptanceTesting.Support;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public interface IHandleTestCompletion
    {
        Task OnTestCompleted(RunSummary summary);
    }
}
