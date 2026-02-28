using NServiceBus;

namespace SampleEndpoint.Handlers;

class SomeReplySagaData : ContainSagaData
{
    public Guid SomeMessageCorrelationId { get; set; }
}
