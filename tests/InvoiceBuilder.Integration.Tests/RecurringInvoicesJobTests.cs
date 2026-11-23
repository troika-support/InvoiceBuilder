using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Recurring;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Integration.Tests.Services;

public sealed class RecurringInvoicesJobTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly IRecurringRepository _schedules = Substitute.For<IRecurringRepository>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRichEmailSender _email = Substitute.For<IRichEmailSender>();
    private readonly InvoiceBuilder.Application.Notifications.INotificationRepository _notifications = Substitute.For<InvoiceBuilder.Application.Notifications.INotificationRepository>();
    private readonly List<Document> _created = [];
    private readonly FakeTimeProvider _clock = new();
    private readonly RecurringSchedule _schedule;

    public RecurringInvoicesJobTests()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var source = Document.CreateDraft(_tenant, _business, DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, now);
        _schedule = RecurringSchedule.Create(_tenant, _business, source.Id, RecurringCadence.Monthly, new DateOnly(2026, 1, 1), null, null, false, now).Value;
        _documents.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _documents.When(d => d.Add(Arg.Any<Document>())).Do(c => _created.Add(c.Arg<Document>()));
        _schedules.ListDueBusinessesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => IsDue(c.Arg<DateOnly>()) ? [(_tenant, _business)] : []);
        _schedules.ListDueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => IsDue(c.Arg<DateOnly>()) ? [_schedule] : []);
    }

    private bool IsDue(DateOnly today) => !_schedule.Paused && _schedule.NextRunDate <= today;

    private RecurringInvoicesJob Job() => new(
        _schedules, _documents, _businesses, Substitute.For<IScopeOverride>(), Substitute.For<ISender>(), _unitOfWork, _email,
        _notifications,
        Options.Create(new AppOptions()), _clock, NullLogger<RecurringInvoicesJob>.Instance);

    private void SetHarareDay(int month, int day) => _clock.SetUtcNow(new DateTimeOffset(2026, month, day, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task R31_rerunning_the_same_day_creates_no_duplicate_draft()
    {
        SetHarareDay(1, 1);
        await Job().RunAsync(CancellationToken.None);
        await Job().RunAsync(CancellationToken.None);
        _created.Count.ShouldBe(1);
    }

    [Fact]
    public async Task R29_monthly_schedule_over_three_months_creates_three_drafts()
    {
        foreach (var month in new[] { 1, 2, 3 })
        {
            SetHarareDay(month, 1);
            await Job().RunAsync(CancellationToken.None);
            await Job().RunAsync(CancellationToken.None);
        }

        _created.Count.ShouldBe(3);
        _schedule.Occurrences.ShouldBe(3);
    }

    [Fact]
    public async Task Drafts_created_add_one_recurring_drafts_ready_notification()
    {
        var business = InvoiceBuilder.Domain.Businesses.Business.Create(_tenant, "B", "B Ltd", "b@b.co", "Harare", _clock.GetUtcNow());
        _businesses.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(business);
        SetHarareDay(1, 1);

        await Job().RunAsync(CancellationToken.None);

        _notifications.Received(1).Add(Arg.Is<InvoiceBuilder.Domain.Notifications.Notification>(n =>
            n.Kind == InvoiceBuilder.Domain.Notifications.NotificationKind.RecurringDraftsReady && n.BusinessId == business.Id));
    }

    [Fact]
    public async Task No_due_schedules_add_no_notification()
    {
        _schedule.Pause(_clock.GetUtcNow());
        SetHarareDay(1, 1);
        await Job().RunAsync(CancellationToken.None);
        _notifications.DidNotReceive().Add(Arg.Any<InvoiceBuilder.Domain.Notifications.Notification>());
    }

    [Fact]
    public async Task R29_paused_schedule_creates_nothing()
    {
        _schedule.Pause(_clock.GetUtcNow());
        SetHarareDay(1, 1);
        await Job().RunAsync(CancellationToken.None);
        _created.ShouldBeEmpty();
    }

    [Fact]
    public async Task R29_schedule_past_its_occurrence_limit_is_paused_not_run()
    {
        _schedule.Update(RecurringCadence.Monthly, null, 1, false, _clock.GetUtcNow()).IsSuccess.ShouldBeTrue();
        SetHarareDay(1, 1);
        await Job().RunAsync(CancellationToken.None);
        SetHarareDay(2, 1);
        await Job().RunAsync(CancellationToken.None);
        _created.Count.ShouldBe(1);
        _schedule.Paused.ShouldBeTrue();
    }
}
