using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

public sealed record QuoteAcceptanceOutcome(string? InvoiceLinkToken);

public sealed record AcceptQuoteAndIssueCommand(string Token, string SignerName, string? Ip, string? UserAgent)
    : ITransactionalCommand<Result<QuoteAcceptanceOutcome>>;

public sealed class AcceptQuoteAndIssueHandler(ISender sender, IDocumentRepository documents, IScopeOverride scope)
    : IRequestHandler<AcceptQuoteAndIssueCommand, Result<QuoteAcceptanceOutcome>>
{
    public async Task<Result<QuoteAcceptanceOutcome>> Handle(AcceptQuoteAndIssueCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var quote = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (quote is null)
        {
            return Result.Failure<QuoteAcceptanceOutcome>(ClientLinkErrors.Invalid);
        }

        var depositOnAcceptance = quote.Type == DocumentType.Quote
            && quote.Instalments.OrderBy(i => i.Position).FirstOrDefault()?.DueKind == InstalmentDueKind.OnAcceptance;

        var accepted = await sender.Send(new AcceptQuoteCommand(request.Token, request.SignerName, request.Ip, request.UserAgent), cancellationToken);
        if (accepted.IsFailure)
        {
            return Result.Failure<QuoteAcceptanceOutcome>(accepted.Error);
        }

        if (!depositOnAcceptance)
        {
            return Result.Success(new QuoteAcceptanceOutcome(null));
        }

        var converted = await sender.Send(new ConvertQuoteToInvoiceCommand(quote.Id), cancellationToken);
        if (converted.IsFailure)
        {
            return Result.Failure<QuoteAcceptanceOutcome>(converted.Error);
        }

        var templates = await sender.Send(new ListPublishedTemplatesQuery(DocumentType.Invoice), cancellationToken);
        if (templates.Count == 0)
        {
            return Result.Failure<QuoteAcceptanceOutcome>(IssueDocumentErrors.TemplateNotFound);
        }

        var issued = await sender.Send(new IssueDocumentCommand(converted.Value, templates[0].VersionId), cancellationToken);
        return issued.IsFailure
            ? Result.Failure<QuoteAcceptanceOutcome>(issued.Error)
            : Result.Success(new QuoteAcceptanceOutcome(issued.Value.LinkToken));
    }
}
