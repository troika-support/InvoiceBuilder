using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

public static class ClientLinkErrors
{
    public static readonly Error Invalid = new("ClientLink.Invalid", "This link isn't valid anymore.");
}

/// <summary>Resolves a client link token to its issued document and scopes the request to it (R26).</summary>
internal static class ClientLinkLoader
{
    public static async Task<Document?> LoadAsync(IDocumentRepository documents, IScopeOverride scope, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var document = await documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), cancellationToken);
        if (document is null || document.Number is null)
        {
            return null;
        }

        scope.UseScope(document.TenantId, document.BusinessId);
        return document;
    }
}

/// <summary>The first open sets Viewed; later opens change nothing (R27).</summary>
public sealed record RecordViewCommand(string Token) : ITransactionalCommand<Result>;

public sealed class RecordViewHandler(IDocumentRepository documents, IScopeOverride scope, IActivityLog activity, TimeProvider time)
    : IRequestHandler<RecordViewCommand, Result>
{
    public async Task<Result> Handle(RecordViewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (document is null)
        {
            return Result.Failure(ClientLinkErrors.Invalid);
        }

        var before = document.Status;
        var result = document.MarkViewed(time.GetUtcNow());
        if (result.IsSuccess && document.Status != before)
        {
            await activity.AddAsync(nameof(Document), document.Id, "DocumentViewed", $"{document.Number?.Value} viewed by client", cancellationToken);
        }

        return result;
    }
}

public sealed record AcceptQuoteCommand(string Token, string SignerName, string? Ip, string? UserAgent) : ITransactionalCommand<Result>;

public sealed class AcceptQuoteHandler(IDocumentRepository documents, IScopeOverride scope, IActivityLog activity, InvoiceBuilder.Application.Notifications.INotificationRepository notifications, TimeProvider time)
    : IRequestHandler<AcceptQuoteCommand, Result>
{
    public async Task<Result> Handle(AcceptQuoteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (document is null)
        {
            return Result.Failure(ClientLinkErrors.Invalid);
        }

        var result = document.Accept(request.SignerName, request.Ip, request.UserAgent, null, time.GetUtcNow());
        if (result.IsSuccess)
        {
            await activity.AddAsync(nameof(Document), document.Id, "QuoteAccepted", $"{document.Number?.Value} accepted by {request.SignerName.Trim()}", cancellationToken);
            notifications.Add(InvoiceBuilder.Domain.Notifications.Notification.Create(document.TenantId, document.BusinessId, InvoiceBuilder.Domain.Notifications.NotificationKind.QuoteAccepted,
                "Quote accepted", $"{document.Number?.Value} was accepted by {request.SignerName.Trim()}.", $"/quotes/{document.Id}", time.GetUtcNow()));
        }

        return result;
    }
}

public sealed record DeclineQuoteCommand(string Token, string SignerName, string? Comment, string? Ip, string? UserAgent) : ITransactionalCommand<Result>;

public sealed class DeclineQuoteHandler(IDocumentRepository documents, IScopeOverride scope, IActivityLog activity, InvoiceBuilder.Application.Notifications.INotificationRepository notifications, TimeProvider time)
    : IRequestHandler<DeclineQuoteCommand, Result>
{
    public async Task<Result> Handle(DeclineQuoteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (document is null)
        {
            return Result.Failure(ClientLinkErrors.Invalid);
        }

        var result = document.Decline(request.SignerName, request.Ip, request.UserAgent, request.Comment, time.GetUtcNow());
        if (result.IsSuccess)
        {
            await activity.AddAsync(nameof(Document), document.Id, "QuoteChangesRequested", $"{document.Number?.Value} declined by {request.SignerName.Trim()}", cancellationToken);
            notifications.Add(InvoiceBuilder.Domain.Notifications.Notification.Create(document.TenantId, document.BusinessId, InvoiceBuilder.Domain.Notifications.NotificationKind.QuoteDeclined,
                "Quote declined", $"{document.Number?.Value} was declined by {request.SignerName.Trim()}.", $"/quotes/{document.Id}", time.GetUtcNow()));
        }

        return result;
    }
}
