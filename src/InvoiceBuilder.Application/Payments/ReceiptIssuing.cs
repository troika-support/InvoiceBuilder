using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;

namespace InvoiceBuilder.Application.Payments;

internal static class ReceiptIssuing
{
    private static readonly TimeSpan HarareOffset = TimeSpan.FromHours(2);

    /// <summary>R18: one numbered receipt per confirmed payment, one line per allocation.</summary>
    public static async Task<Result<Document>> IssueAsync(INumberSequenceRepository numbers,
        Payment payment,
        List<(Document Invoice, decimal Amount)> invoices,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var first = invoices[0].Invoice;
        var receipt = Document.CreateDraft(
            payment.TenantId, payment.BusinessId, DocumentType.Receipt, payment.ClientId, payment.Currency, now, first.Id);

        foreach (var (invoice, amount) in invoices)
        {
            var line = receipt.AddLine(new LineInput($"Payment for {invoice.Number!.Value}", null, 1m, amount), now);
            if (line.IsFailure)
            {
                return Result.Failure<Document>(line.Error);
            }
        }

        var number = await numbers.AllocateAsync(payment.BusinessId, DocumentType.Receipt, cancellationToken);
        if (number.IsFailure)
        {
            return Result.Failure<Document>(number.Error);
        }

        var issued = receipt.Issue(
            new IssueRequest(
                number.Value,
                DateOnly.FromDateTime(now.ToOffset(HarareOffset).DateTime),
                0,
                first.ClientSnapshot,
                first.BusinessSnapshot,
                first.TemplateVersionId!.Value),
            now);
        return issued.IsFailure ? Result.Failure<Document>(issued.Error) : Result.Success(receipt);
    }
}
