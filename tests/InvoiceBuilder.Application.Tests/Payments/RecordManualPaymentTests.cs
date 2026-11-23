using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class RecordManualPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly INumberSequenceRepository _numbers = Substitute.For<INumberSequenceRepository>();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly Guid _client = Guid.NewGuid();

    private Document Invoice(string number, decimal amount)
    {
        var invoice = Document.CreateDraft(_tenant, _business, DocumentType.Invoice, _client, CurrencyCode.Usd, Now);
        invoice.AddLine(new LineInput("Item", null, 1m, amount, 0m, null), Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(new IssueRequest(DocumentNumber.Create(number).Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid()), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _documents.ListCreditNotesAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(0m);
        return invoice;
    }

    private RecordManualPaymentHandler Handler()
    {
        _numbers.AllocateAsync(_business, DocumentType.Receipt, Arg.Any<CancellationToken>()).Returns(Result.Success(DocumentNumber.Create("RCT-0001").Value));
        return new RecordManualPaymentHandler(_documents, _payments, _numbers, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task R16_over_allocation_is_rejected()
    {
        var a = Invoice("INV-0001", 100m);
        var result = await Handler().Handle(new RecordManualPaymentCommand(_client, 100m, new DateOnly(2026, 10, 23), PaymentMethod.Cash, [new ManualAllocation(a.Id, 150m)]), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task R18_payment_across_two_invoices_issues_one_receipt_and_marks_both_paid()
    {
        var a = Invoice("INV-0001", 100m);
        var b = Invoice("INV-0002", 50m);
        var result = await Handler().Handle(new RecordManualPaymentCommand(_client, 150m, new DateOnly(2026, 10, 23), PaymentMethod.BankTransfer, [new ManualAllocation(a.Id, 100m), new ManualAllocation(b.Id, 50m)]), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ReceiptNumber.ShouldBe("RCT-0001");
        a.Status.ShouldBe(DocumentStatus.Paid);
        b.Status.ShouldBe(DocumentStatus.Paid);
        _documents.Received(1).Add(Arg.Is<Document>(d => d.Type == DocumentType.Receipt && d.Lines.Count == 2));
    }

    [Fact]
    public async Task R17_part_payment_marks_invoice_part_paid()
    {
        var a = Invoice("INV-0001", 100m);
        var result = await Handler().Handle(new RecordManualPaymentCommand(_client, 40m, new DateOnly(2026, 10, 23), PaymentMethod.Cash, [new ManualAllocation(a.Id, 40m)]), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        a.Status.ShouldBe(DocumentStatus.PartPaid);
    }
}
