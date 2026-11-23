using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

/// <summary>The issued document's client link, e.g. for "Copy client link" (R26). Older documents get one stored on first use.</summary>
public sealed record GetClientLinkCommand(Guid DocumentId) : ITransactionalCommand<Result<string>>;

public sealed class GetClientLinkHandler(ICurrentBusiness currentBusiness, IDocumentRepository documents, ClientLinkService links, TimeProvider time)
    : IRequestHandler<GetClientLinkCommand, Result<string>>
{
    public async Task<Result<string>> Handle(GetClientLinkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<string>(loaded.Error);
        }

        if (loaded.Value.Number is null)
        {
            return Result.Failure<string>(ClientLinkRequestErrors.NotIssued);
        }

        var token = links.EnsureToken(loaded.Value, null, time.GetUtcNow());
        return token.IsSuccess ? Result.Success(links.Url(token.Value)) : Result.Failure<string>(token.Error);
    }
}

/// <summary>
/// Owner action: replaces the client link so every link sent so far stops working (a link shared by mistake).
/// The stored PDF carries the old link in its QR code, so it is rebuilt after the change commits.
/// </summary>
public sealed record RevokeClientLinkCommand(Guid DocumentId) : IRequest<Result<string>>;

public sealed class RevokeClientLinkHandler(ISender sender, IDocumentRepository documents, IFileStore files, IPdfJobScheduler pdfJobs)
    : IRequestHandler<RevokeClientLinkCommand, Result<string>>
{
    public async Task<Result<string>> Handle(RevokeClientLinkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var revoked = await sender.Send(new RevokeClientLinkInTransactionCommand(request.DocumentId), cancellationToken);
        if (revoked.IsFailure)
        {
            return revoked;
        }

        var document = await documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document?.TemplateVersionId is { } versionId)
        {
            await files.DeleteAsync(PdfKeys.For(document.Id, versionId), cancellationToken);
            pdfJobs.Enqueue(document.Id);
        }

        return revoked;
    }
}

public sealed record RevokeClientLinkInTransactionCommand(Guid DocumentId) : ITransactionalCommand<Result<string>>;

public sealed class RevokeClientLinkInTransactionHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    ClientLinkService links,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<RevokeClientLinkInTransactionCommand, Result<string>>
{
    public async Task<Result<string>> Handle(RevokeClientLinkInTransactionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<string>(loaded.Error);
        }

        var document = loaded.Value;
        if (document.Number is null)
        {
            return Result.Failure<string>(ClientLinkRequestErrors.NotIssued);
        }

        var token = links.Revoke(document, time.GetUtcNow());
        if (token.IsFailure)
        {
            return Result.Failure<string>(token.Error);
        }

        await activity.AddAsync(nameof(Document), document.Id, "ClientLinkRevoked", $"Client link for {document.Number.Value} replaced; earlier links no longer work.", cancellationToken);
        return Result.Success(links.Url(token.Value));
    }
}

/// <summary>What the Send dialog needs for an issued document, so lists can offer "Send again".</summary>
public sealed record SendTarget(Guid DocumentId, string Number, string Amount, string ClientName, string? ClientEmail, string? ClientPhone);

public sealed record GetSendTargetQuery(Guid DocumentId) : IRequest<Result<SendTarget>>;

public sealed class GetSendTargetHandler(ICurrentBusiness currentBusiness, IDocumentRepository documents, IClientRepository clients, IDisplayFormatter format)
    : IRequestHandler<GetSendTargetQuery, Result<SendTarget>>
{
    public async Task<Result<SendTarget>> Handle(GetSendTargetQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<SendTarget>(loaded.Error);
        }

        var document = loaded.Value;
        if (document.Number is null || document.Status == DocumentStatus.Void)
        {
            return Result.Failure<SendTarget>(ClientLinkRequestErrors.NotIssued);
        }

        var client = await clients.GetByIdAsync(document.ClientId, cancellationToken);
        return Result.Success(new SendTarget(
            document.Id,
            document.Number.Value,
            format.Money(document.Total, document.Currency.Code),
            client?.Name ?? string.Empty,
            client?.Email,
            string.IsNullOrWhiteSpace(client?.WhatsApp) ? client?.Phone : client.WhatsApp));
    }
}

public static class ClientLinkRequestErrors
{
    public static readonly Error NotIssued = new("ClientLink.NotIssued", "Issue the document first; drafts don't have a client link.");
}
