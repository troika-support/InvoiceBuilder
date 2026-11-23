using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Application.Messaging;

/// <summary>Emails the business owner when a client accepts a quote. Best effort: a failed send never undoes the acceptance.</summary>
public sealed record NotifyQuoteAcceptedCommand(string Token) : IRequest<Result>;

public sealed class NotifyQuoteAcceptedHandler(
    IDocumentRepository documents,
    IScopeOverride scope,
    IBusinessRepository businesses,
    IClientRepository clients,
    IRichEmailSender email,
    IOptions<AppOptions> app,
    ILogger<NotifyQuoteAcceptedHandler> logger) : IRequestHandler<NotifyQuoteAcceptedCommand, Result>
{
    public async Task<Result> Handle(NotifyQuoteAcceptedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var quote = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (quote is null || quote.Number is null)
        {
            return Result.Success();
        }

        var business = await businesses.GetByIdAsync(quote.BusinessId, cancellationToken);
        if (business is null || string.IsNullOrWhiteSpace(business.Email))
        {
            return Result.Success();
        }

        var client = await clients.GetByIdAsync(quote.ClientId, cancellationToken);
        var link = $"{(app.Value.PublicBaseUrl ?? "https://localhost").TrimEnd('/')}/quotes/{quote.Id}";
        var content = EmailContent.QuoteAccepted(business.TradingName, quote.Number.Value, client?.Name ?? "Your client", link);
        try
        {
            await email.SendAsync(new EmailMessage(business.Email, content.Subject, content.Html, content.Text), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, quote.Id);
        }

        return Result.Success();
    }

    private static readonly Action<ILogger, Guid, Exception?> LogFailedMessage =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1), "Quote accepted email failed for {QuoteId}");

    private static void LogFailed(ILogger logger, Exception exception, Guid quoteId) => LogFailedMessage(logger, quoteId, exception);
}

