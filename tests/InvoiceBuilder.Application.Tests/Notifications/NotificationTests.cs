using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Notifications;
using InvoiceBuilder.Domain.Notifications;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Notifications;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly INotificationRepository _repo = Substitute.For<INotificationRepository>();
    private readonly ICurrentBusiness _current = Substitute.For<ICurrentBusiness>();
    private readonly FakeTimeProvider _clock = new(Now);

    private Notification Make() =>
        Notification.Create(_tenant, _business, NotificationKind.PaymentConfirmed, "Payment received", "USD 10", "/payments", Now);

    [Fact]
    public void Dismiss_twice_keeps_the_first_time()
    {
        var n = Make();
        n.Dismiss(Now);
        n.Dismiss(Now.AddHours(1));

        n.IsDismissed.ShouldBeTrue();
        n.DismissedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task List_maps_active_notifications()
    {
        var n = Make();
        _repo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([n]);

        var rows = await new ListNeedsAttentionHandler(_repo).Handle(new ListNeedsAttentionQuery(), CancellationToken.None);

        rows.Count.ShouldBe(1);
        rows[0].Title.ShouldBe("Payment received");
        rows[0].LinkPath.ShouldBe("/payments");
    }

    [Fact]
    public async Task Dismiss_marks_own_business_notification_dismissed()
    {
        var n = Make();
        _current.BusinessId.Returns(_business);
        _repo.GetByIdAsync(n.Id, Arg.Any<CancellationToken>()).Returns(n);

        var result = await new DismissNotificationHandler(_repo, _current, _clock)
            .Handle(new DismissNotificationCommand(n.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        n.IsDismissed.ShouldBeTrue();
    }

    [Fact]
    public async Task Dismiss_other_business_notification_is_not_found()
    {
        var n = Make();
        _current.BusinessId.Returns(Guid.NewGuid());
        _repo.GetByIdAsync(n.Id, Arg.Any<CancellationToken>()).Returns(n);

        var result = await new DismissNotificationHandler(_repo, _current, _clock)
            .Handle(new DismissNotificationCommand(n.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        n.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public async Task Dismiss_unknown_notification_is_not_found()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        var result = await new DismissNotificationHandler(_repo, _current, _clock)
            .Handle(new DismissNotificationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }
}
