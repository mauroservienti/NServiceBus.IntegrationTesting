using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public interface IRemoteMessageSessionProxy
    {
        Task Send<TMessage>(string destination, TMessage message);
        Task Send<TMessage>(TMessage message);
    }
}