namespace NServiceBus.IntegrationTesting.Messages
{
    class CreateSagaInstance
    {
        public string CorrelationPropertyName { get; set; }
        public object CorrelationPropertyValue { get; set; }
        public IContainSagaData SagaData { get; set; }
    }
}
