using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

/// <summary>Owner action: turns an accepted quote into a linked Draft invoice, once (R8).</summary>
public sealed record ConvertQuoteToInvoiceCommand(Guid QuoteId) : ITransactionalCommand<Result<Guid>>;

public sealed class ConvertQuoteToInvoiceHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<ConvertQuoteToInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ConvertQuoteToInvoiceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var quote = await documents.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null || currentBusiness.BusinessId != quote.BusinessId)
        {
            return Result.Failure<Guid>(new Error("Document.NotFound", "Document not found."));
        }

        var converted = quote.ConvertToInvoice(time.GetUtcNow());
        if (converted.IsFailure)
        {
            return Result.Failure<Guid>(converted.Error);
        }

        documents.Add(converted.Value);
        await activity.AddAsync(nameof(Document), quote.Id, "QuoteConverted", $"{quote.Number?.Value} converted to a draft invoice", cancellationToken);
        return Result.Success(converted.Value.Id);
    }
}
