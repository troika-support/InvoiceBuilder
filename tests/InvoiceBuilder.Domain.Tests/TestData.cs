using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

internal static class TestData
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid BusinessId = Guid.NewGuid();
    public static readonly Guid ClientId = Guid.NewGuid();
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    public static TaxRate Vat(decimal percent = 15m) =>
        TaxRate.Create(TenantId, BusinessId, "VAT", TaxPercent.Create(percent).Value, new DateOnly(2026, 1, 1), null, Now);

    public static Document DraftInvoice(CurrencyCode? currency = null) =>
        Document.CreateDraft(TenantId, BusinessId, DocumentType.Invoice, ClientId, currency ?? CurrencyCode.Usd, Now);

    public static Document DraftQuote() =>
        Document.CreateDraft(TenantId, BusinessId, DocumentType.Quote, ClientId, CurrencyCode.Usd, Now);

    public static LineInput Line(decimal quantity, decimal unitPrice, TaxRate? taxRate = null) =>
        new("Item", null, quantity, unitPrice, 0m, taxRate);

    public static IssueRequest NewIssueRequest(string number, DateOnly issueDate, int termsDays = 14) =>
        new(DocumentNumber.Create(number).Value, issueDate, termsDays, "{}", "{}", Guid.NewGuid());

    public static Document IssuedInvoice(decimal unitPrice, DateOnly issueDate, TaxRate? taxRate = null)
    {
        var invoice = DraftInvoice();
        invoice.AddLine(Line(1m, unitPrice, taxRate), Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(NewIssueRequest("INV-0001", issueDate), Now).IsSuccess.ShouldBeTrue();
        return invoice;
    }

    public static Document IssuedQuote()
    {
        var quote = DraftQuote();
        quote.AddLine(Line(1m, 100m), Now).IsSuccess.ShouldBeTrue();
        quote.Issue(NewIssueRequest("QUO-0001", new DateOnly(2026, 10, 23)), Now).IsSuccess.ShouldBeTrue();
        return quote;
    }

    public static Document DraftCreditNote(Document invoice, decimal unitPrice, TaxRate? taxRate, string? reason = "Returned goods", CurrencyCode? currency = null)
    {
        var note = Document.CreateDraft(TenantId, BusinessId, DocumentType.CreditNote, ClientId, currency ?? invoice.Currency, Now, invoice.Id);
        if (reason is not null)
        {
            note.SetCreditReason(reason, Now).IsSuccess.ShouldBeTrue();
        }

        note.AddLine(Line(1m, unitPrice, taxRate), Now).IsSuccess.ShouldBeTrue();
        return note;
    }
}
