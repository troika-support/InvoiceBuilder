using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

public static class DocumentErrors
{
    public static Error NotEditable { get; } = new("Document.NotEditable", "Only a draft document can be edited.");

    public static Error NoLines { get; } = new("Document.NoLines", "Add at least one line before issuing.");

    public static Error LineNotFound { get; } = new("Document.LineNotFound", "That line does not exist.");

    public static Error InvalidTransition(DocumentType type, DocumentStatus from, DocumentStatus to) =>
        new("Document.InvalidTransition", $"A {type} cannot move from {from} to {to}.");

    public static Error WrongType(DocumentType expected) =>
        new("Document.WrongType", $"This action only applies to a {expected}.");

    public static Error QuoteExpired { get; } = new("Quote.Expired", "This quote has expired.");

    public static Error QuoteAlreadyConverted { get; } = new("Quote.AlreadyConverted", "This quote has already been converted.");

    public static Error QuoteNotAccepted { get; } = new("Quote.NotAccepted", "Only an accepted quote can be converted.");

    public static Error SignerRequired { get; } = new("Quote.SignerRequired", "The signer's full name is required.");

    public static Error InvalidLine(string message) => new("Line.Invalid", message);

    public static Error DiscountTooLarge { get; } = new("Document.DiscountTooLarge", "The discount cannot exceed the document total.");

    public static Error TermsInvalid { get; } = new("Document.TermsInvalid", "Payment terms cannot be negative.");

    public static Error DateBeforeIssue { get; } = new("Document.DateBeforeIssue", "The due date or valid-until date cannot be before the issue date.");

    public static Error DueKindNotAllowed(DocumentType type) => type == DocumentType.Invoice
        ? new("Instalment.DueKindNotAllowed", "\"On acceptance\" only applies to quotes. Use \"On issue\" on an invoice.")
        : new("Instalment.DueKindNotAllowed", "\"On issue\" only applies to invoices. Use \"On acceptance\" on a quote.");
}
