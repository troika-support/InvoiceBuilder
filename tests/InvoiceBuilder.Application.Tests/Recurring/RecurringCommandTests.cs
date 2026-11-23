using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Recurring;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Recurring;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Recurring;

public sealed class RecurringCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IRecurringRepository _schedules = Substitute.For<IRecurringRepository>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _currentBusiness = Substitute.For<ICurrentBusiness>();

    public RecurringCommandTests()
    {
        _currentTenant.TenantId.Returns(_tenant);
        _currentBusiness.BusinessId.Returns(_business);
    }

    private MakeRecurringHandler Handler() =>
        new(_currentTenant, _currentBusiness, _documents, _schedules, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));

    private Document Draft(DocumentType type, Guid? business = null) =>
        Document.CreateDraft(_tenant, business ?? _business, type, Guid.NewGuid(), CurrencyCode.Usd, Now);

    [Fact]
    public async Task R29_draft_invoice_cannot_be_made_recurring()
    {
        var draft = Draft(DocumentType.Invoice);
        _documents.GetByIdAsync(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);
        var result = await Handler().Handle(new MakeRecurringCommand(draft.Id, RecurringCadence.Monthly, new DateOnly(2026, 11, 1), null, null, false), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RecurringErrors.SourceInvalid.Code);
        _schedules.DidNotReceive().Add(Arg.Any<RecurringSchedule>());
    }

    [Fact]
    public async Task R2_source_from_another_business_is_rejected()
    {
        var other = Draft(DocumentType.Invoice, Guid.NewGuid());
        _documents.GetByIdAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);
        var result = await Handler().Handle(new MakeRecurringCommand(other.Id, RecurringCadence.Monthly, new DateOnly(2026, 11, 1), null, null, false), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task R29_pause_and_resume_toggle_the_schedule()
    {
        var schedule = RecurringSchedule.Create(_tenant, _business, Guid.NewGuid(), RecurringCadence.Weekly, new DateOnly(2026, 11, 1), null, null, false, Now).Value;
        _schedules.GetByIdAsync(schedule.Id, Arg.Any<CancellationToken>()).Returns(schedule);
        var handler = new SetRecurringPausedHandler(_schedules, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));
        (await handler.Handle(new SetRecurringPausedCommand(schedule.Id, true), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        schedule.Paused.ShouldBeTrue();
        await handler.Handle(new SetRecurringPausedCommand(schedule.Id, false), CancellationToken.None);
        schedule.Paused.ShouldBeFalse();
    }
}
