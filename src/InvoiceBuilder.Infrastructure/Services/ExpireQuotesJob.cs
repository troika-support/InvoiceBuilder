using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>
/// Daily quote expiry (R7). Open quotes past ValidUntil become Expired; a second run changes nothing because
/// expired quotes are no longer open. Runs per business so every query stays scoped.
/// </summary>
public sealed partial class ExpireQuotesJob(
    IDocumentRepository documents,
    IScopeOverride scope,
    IUnitOfWork unitOfWork,
    IActivityLog activity,
    TimeProvider timeProvider,
    ILogger<ExpireQuotesJob> logger)
{
    private static readonly TimeSpan Harare = TimeSpan.FromHours(2);

    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime((now + Harare).DateTime);
        foreach (var (tenantId, businessId) in await documents.ListBusinessesWithExpiredQuotesAsync(today, cancellationToken))
        {
            try
            {
                scope.UseScope(tenantId, businessId);
                foreach (var quote in await documents.ListExpiredQuotesAsync(today, cancellationToken))
                {
                    if (quote.Expire(now).IsSuccess)
                    {
                        await activity.AddAsync("Document", quote.Id, "QuoteExpired", $"Quote {quote.Number?.Value} expired.", cancellationToken);
                    }
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBusinessFailed(logger, ex, businessId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Quote expiry failed for business {BusinessId}")]
    private static partial void LogBusinessFailed(ILogger logger, Exception exception, Guid businessId);
}
