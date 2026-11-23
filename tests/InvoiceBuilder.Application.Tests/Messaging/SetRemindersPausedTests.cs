using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Messaging;

public sealed class SetRemindersPausedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly ICurrentBusiness _current = Substitute.For<ICurrentBusiness>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();

    public SetRemindersPausedTests() => _current.BusinessId.Returns(_business);

    private SetRemindersPausedHandler Handler() => new(_current, _documents, _activity, new FakeTimeProvider(Now));

    private Document Stored(DocumentType type, Guid? business = null)
    {
        var doc = Document.CreateDraft(_tenant, business ?? _business, type, Guid.NewGuid(), CurrencyCode.Usd, Now);
        _documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        return doc;
    }

    [Fact]
    public async Task R30_pause_then_resume_toggles_flag_and_audits_each()
    {
        var invoice = Stored(DocumentType.Invoice);

        (await Handler().Handle(new SetRemindersPausedCommand(invoice.Id, true), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        invoice.RemindersPaused.ShouldBeTrue();
        (await Handler().Handle(new SetRemindersPausedCommand(invoice.Id, false), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        invoice.RemindersPaused.ShouldBeFalse();

        await _activity.Received(1).AddAsync(nameof(Document), invoice.Id, "RemindersPaused", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _activity.Received(1).AddAsync(nameof(Document), invoice.Id, "RemindersResumed", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R30_other_business_invoice_is_not_found_and_unchanged()
    {
        var invoice = Stored(DocumentType.Invoice, Guid.NewGuid());

        var result = await Handler().Handle(new SetRemindersPausedCommand(invoice.Id, true), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        invoice.RemindersPaused.ShouldBeFalse();
    }

    [Fact]
    public async Task R30_quotes_cannot_have_reminders_paused()
    {
        var quote = Stored(DocumentType.Quote);

        (await Handler().Handle(new SetRemindersPausedCommand(quote.Id, true), CancellationToken.None)).IsFailure.ShouldBeTrue();
    }
}
