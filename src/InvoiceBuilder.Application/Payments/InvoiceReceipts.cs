using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Payments;

public sealed record ReceiptRow(Guid Id, string Number, DateOnly? IssuedOn, decimal Total, string Currency);

public sealed record GetInvoiceReceiptsQuery(Guid InvoiceId) : IRequest<Result<IReadOnlyList<ReceiptRow>>>;

public sealed class GetInvoiceReceiptsHandler(IDocumentRepository documents)
    : IRequestHandler<GetInvoiceReceiptsQuery, Result<IReadOnlyList<ReceiptRow>>>
{
    public async Task<Result<IReadOnlyList<ReceiptRow>>> Handle(GetInvoiceReceiptsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var receipts = await documents.ListReceiptsAsync(request.InvoiceId, cancellationToken);
        IReadOnlyList<ReceiptRow> rows = [.. receipts.Where(r => r.Number is not null).Select(r => new ReceiptRow(r.Id, r.Number!.Value.ToString(), r.IssueDate, r.Total, r.Currency.ToString()))];
        return Result.Success(rows);
    }
}
