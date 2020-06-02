using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting
{
    public static class MessageSessionExtensions
    {
        public static Task CreateSagaInstance<TSagaData>(this IMessageSession messageSession, string sagaOwnerEndpoint, Guid sagaId, TSagaData sagaData, string originator = null, string originatingMessageId = null) where TSagaData : IContainSagaData
        {
            sagaData.Id = sagaId;
            sagaData.Originator = originator;
            sagaData.OriginalMessageId = originatingMessageId;

            return messageSession.Send(sagaOwnerEndpoint, new Messages.CreateSagaInstance() 
            { 
                SagaData = sagaData
            });
        }
    }
}
