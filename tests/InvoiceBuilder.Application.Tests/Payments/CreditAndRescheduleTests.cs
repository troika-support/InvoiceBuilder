using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class CreditAndRescheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly INumberSequenceRepository _numbers = Substitute.For<INumberSequenceRepository>();

    private Document Invoice(decimal amount, DateTimeOffset issuedAt, decimal paid = 0m, IReadOnlyList<InstalmentDraft>? plan = null)
    {
        var invoice = Document.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, issuedAt);
        invoice.AddLine(new LineInput("Item", null, 1m, amount, 0m, null), issuedAt).IsSuccess.ShouldBeTrue();
        if (plan is not null)
        {
            invoice.SetPlan(plan, InstalmentFrequency.Monthly, issuedAt).IsSuccess.ShouldBeTrue();
        }

        invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, DateOnly.FromDateTime(issuedAt.UtcDateTime), 14, "{}", "{}", Guid.NewGuid()), issuedAt).IsSuccess.ShouldBeTrue();
        _documents.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _documents.ListCreditNotesAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(paid);
        return invoice;
    }

    private IssueCreditNoteHandler CreditHandler()
    {
        _numbers.AllocateAsync(Arg.Any<Guid>(), DocumentType.CreditNote, Arg.Any<CancellationToken>()).Returns(Result.Success(DocumentNumber.Create("CRN-0001").Value));
        return new IssueCreditNoteHandler(_documents, _payments, _numbers, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task R4_full_credit_voids_the_invoice()
    {
        var invoice = Invoice(100m, Now);
        var result = await CreditHandler().Handle(new IssueCreditNoteCommand(invoice.Id, null, "Cancelled"), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Void);
    }

    [Fact]
    public async Task R17_partial_credit_then_payment_marks_paid_at_the_reduced_balance()
    {
        var invoice = Invoice(100m, Now, paid: 60m);
        var result = await CreditHandler().Handle(new IssueCreditNoteCommand(invoice.Id, 40m, "Discount"), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Paid);
    }

    [Fact]
    public async Task R13b_credit_note_reason_is_required_and_amount_cannot_exceed_invoice()
    {
        var invoice = Invoice(100m, Now);
        (await CreditHandler().Handle(new IssueCreditNoteCommand(invoice.Id, null, " "), CancellationToken.None)).IsFailure.ShouldBeTrue();
        (await CreditHandler().Handle(new IssueCreditNoteCommand(invoice.Id, 150m, "Too much"), CancellationToken.None)).Error.Code.ShouldBe("CreditNote.ExceedsInvoice");
    }

    [Fact]
    public async Task R13b_credit_note_more_than_12_months_after_the_invoice_is_rejected()
    {
        var invoice = Invoice(100m, Now.AddMonths(-13));
        var result = await CreditHandler().Handle(new IssueCreditNoteCommand(invoice.Id, null, "Late"), CancellationToken.None);
        result.Error.Code.ShouldBe("CreditNote.TooLate");
    }

    [Fact]
    public async Task R36_reschedule_of_a_paid_instalment_is_rejected()
    {
        var invoice = Invoice(1600m, Now, paid: 800m, plan: [new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m), new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m)]);
        var handler = new RescheduleInstalmentsHandler(_documents, _payments, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));
        var result = await handler.Handle(new RescheduleInstalmentsCommand(invoice.Id, [new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 11, 20), 800m), new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m)], "Client asked"), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task R37_payment_of_1000_on_a_3x800_plan_pays_first_and_part_pays_second()
    {
        var invoice = Invoice(2400m, Now, paid: 1000m, plan: [new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m), new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m), new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2027, 1, 1), 800m)]);
        var states = invoice.GetInstalmentStates(1000m, 0m, new DateOnly(2026, 10, 23));
        states[0].Status.ToString().ShouldBe("Paid");
        states[1].Status.ToString().ShouldBe("PartPaid");
    }
}
