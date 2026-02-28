using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests;

[TestFixture]
public class ObserveContextTests
{
    [Test]
    public void WhenAllAsync_throws_when_no_conditions_are_registered()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(() => context.WhenAllAsync());
    }

    [Test]
    public void MessageFailed_throws_on_second_call()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);
        context.MessageFailed();

        Assert.Throws<InvalidOperationException>(() => context.MessageFailed());
    }

    [Test]
    public void HandlerInvoked_throws_when_MessageFailed_is_registered()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);
        context.MessageFailed();

        Assert.Throws<InvalidOperationException>(() => context.HandlerInvoked("MyHandler"));
    }

    [Test]
    public void SagaInvoked_throws_when_MessageFailed_is_registered()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);
        context.MessageFailed();

        Assert.Throws<InvalidOperationException>(() => context.SagaInvoked("MySaga"));
    }

    [Test]
    public void MessageDispatched_throws_when_MessageFailed_is_registered()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);
        context.MessageFailed();

        Assert.Throws<InvalidOperationException>(() => context.MessageDispatched("MyMessage"));
    }

    [Test]
    public void MessageFailed_throws_when_success_conditions_are_registered()
    {
        var grpcService = new TestHostGrpcService();
        var context = new ObserveContext(grpcService, "c1", CancellationToken.None);
        context.HandlerInvoked("MyHandler");

        Assert.Throws<InvalidOperationException>(() => context.MessageFailed());
    }
}
