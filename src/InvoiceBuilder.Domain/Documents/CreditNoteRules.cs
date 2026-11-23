using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>Credit-note rules that phase 2 relies on (R13b). Pure.</summary>
public static class CreditNoteRules
{
    public static Error ReasonRequired { get; } = new("CreditNote.ReasonRequired", "A credit note needs a reason.");

    public static Error WrongInvoice { get; } = new("CreditNote.WrongInvoice", "A credit note must reference the invoice it credits.");

    public static Error CurrencyMismatch { get; } = new("CreditNote.CurrencyMismatch", "A credit note must use the invoice's currency.");

    public static Error TaxRateNotOnInvoice { get; } = new("CreditNote.TaxRateNotOnInvoice", "Credit note tax rates must be ones used on the invoice.");

    public static Error ExceedsInvoice { get; } = new("CreditNote.ExceedsInvoice", "Credit notes cannot total more than the invoice.");

    public static Error TooLate { get; } = new("CreditNote.TooLate", "A credit note cannot be issued more than 12 months after the invoice.");

    public static Error InvoiceNotIssued { get; } = new("CreditNote.InvoiceNotIssued", "Only an issued invoice can be credited.");

    /// <param name="invoice">The issued invoice being credited.</param>
    /// <param name="existingCreditNotes">Other credit notes already raised against the invoice.</param>
    /// <param name="creditNote">The credit note being validated.</param>
    /// <param name="issueDate">The date the credit note would be issued.</param>
    public static Result Validate(
        Document invoice,
        IReadOnlyCollection<Document> existingCreditNotes,
        Document creditNote,
        DateOnly issueDate)
    {
        if (invoice.Type != DocumentType.Invoice || invoice.Status == DocumentStatus.Draft || invoice.IssueDate is null)
        {
            return Result.Failure(InvoiceNotIssued);
        }

        if (creditNote.Type != DocumentType.CreditNote || creditNote.SourceDocumentId != invoice.Id)
        {
            return Result.Failure(WrongInvoice);
        }

        if (string.IsNullOrWhiteSpace(creditNote.CreditReason))
        {
            return Result.Failure(ReasonRequired);
        }

        if (creditNote.Currency != invoice.Currency)
        {
            return Result.Failure(CurrencyMismatch);
        }

        var invoiceRates = invoice.Lines.Select(l => (l.TaxRateId, l.TaxPercentSnapshot)).ToHashSet();
        if (creditNote.Lines.Any(l => !invoiceRates.Contains((l.TaxRateId, l.TaxPercentSnapshot))))
        {
            return Result.Failure(TaxRateNotOnInvoice);
        }

        if (existingCreditNotes.Sum(n => n.Total) + creditNote.Total > invoice.Total)
        {
            return Result.Failure(ExceedsInvoice);
        }

        if (issueDate > invoice.IssueDate.Value.AddMonths(12))
        {
            return Result.Failure(TooLate);
        }

        return Result.Success();
    }
}
