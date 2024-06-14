using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public static class MessageSessionExtensions
    {
        public static Task TriggerTimeoutFor<TSaga>(this IMessageSession messageSession, string destinationEndpointName, string sagaUniqueIdentifier, object timeoutInstance) 
        {
            var sendOptions = new SendOptions();
            sendOptions.SetDestination(destinationEndpointName);
            sendOptions.SetHeader(Headers.SagaType, typeof(TSaga).AssemblyQualifiedName);
            sendOptions.SetHeader(Headers.IsSagaTimeoutMessage, bool.TrueString);
            sendOptions.SetHeader(Headers.SagaId, sagaUniqueIdentifier);

            return messageSession.Send(timeoutInstance, sendOptions);
        }
    }
}
