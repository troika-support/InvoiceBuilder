using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>Allowed lifecycle transitions (R4, R5, R7, R8). Pure; no state of its own.</summary>
public static class DocumentStateMachine
{
    public static bool IsEditable(DocumentStatus status) => status == DocumentStatus.Draft;

    public static bool CanTransition(DocumentType type, DocumentStatus from, DocumentStatus to) =>
        Allowed(type, from).Contains(to);

    public static Result EnsureTransition(DocumentType type, DocumentStatus from, DocumentStatus to) =>
        CanTransition(type, from, to)
            ? Result.Success()
            : Result.Failure(DocumentErrors.InvalidTransition(type, from, to));

    private static DocumentStatus[] Allowed(DocumentType type, DocumentStatus from) => type switch
    {
        DocumentType.Quote => from switch
        {
            DocumentStatus.Draft => [DocumentStatus.Issued],
            DocumentStatus.Issued => [DocumentStatus.Sent, DocumentStatus.Viewed, DocumentStatus.Accepted, DocumentStatus.Declined, DocumentStatus.Expired],
            DocumentStatus.Sent => [DocumentStatus.Viewed, DocumentStatus.Accepted, DocumentStatus.Declined, DocumentStatus.Expired],
            DocumentStatus.Viewed => [DocumentStatus.Accepted, DocumentStatus.Declined, DocumentStatus.Expired],
            DocumentStatus.Accepted => [DocumentStatus.Converted],
            _ => [],
        },
        DocumentType.Invoice => from switch
        {
            DocumentStatus.Draft => [DocumentStatus.Issued],
            DocumentStatus.Issued => [DocumentStatus.Sent, DocumentStatus.Viewed, DocumentStatus.PartPaid, DocumentStatus.Paid, DocumentStatus.Void],
            DocumentStatus.Sent => [DocumentStatus.Viewed, DocumentStatus.PartPaid, DocumentStatus.Paid, DocumentStatus.Void],
            DocumentStatus.Viewed => [DocumentStatus.PartPaid, DocumentStatus.Paid, DocumentStatus.Void],
            DocumentStatus.PartPaid => [DocumentStatus.Paid, DocumentStatus.Void],
            _ => [],
        },
        DocumentType.Receipt or DocumentType.CreditNote => from == DocumentStatus.Draft ? [DocumentStatus.Issued] : [],
        _ => [],
    };
}
