using InvoiceBuilder.Infrastructure.Email;
using InvoiceBuilder.Infrastructure.Services;

namespace InvoiceBuilder.Integration.Tests.Messaging;

public sealed class SendRetryTests
{
    [Fact]
    public void Outages_where_nothing_was_accepted_are_retried()
    {
        SendOutboundMessageJob.IsTemporary(new MessageProviderUnavailableException("503"), CancellationToken.None).ShouldBeTrue();
        SendOutboundMessageJob.IsTemporary(new HttpRequestException("reset"), CancellationToken.None).ShouldBeTrue();
        SendOutboundMessageJob.IsTemporary(new TaskCanceledException("timeout"), CancellationToken.None).ShouldBeTrue();
    }

    [Fact]
    public void Rejections_and_shutdowns_are_not_retried()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        SendOutboundMessageJob.IsTemporary(new InvalidOperationException("Email provider rejected the message (400)."), CancellationToken.None).ShouldBeFalse();
        SendOutboundMessageJob.IsTemporary(new TaskCanceledException("stopping"), cancelled.Token).ShouldBeFalse();
    }

    [Fact]
    public void Three_retries_with_growing_gaps()
    {
        SendOutboundMessageJob.RetryDelays.Length.ShouldBe(3);
        SendOutboundMessageJob.RetryDelays.ShouldBeInOrder();
    }
}
