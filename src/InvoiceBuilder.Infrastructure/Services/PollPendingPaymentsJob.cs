using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Payments;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>
/// Every 2 minutes (paynow.md section 3, step 6). Pending payments older than 1 minute and younger than 24 hours are
/// polled and confirmed through the same idempotent command as the webhook; older ones are failed.
/// </summary>
public sealed partial class PollPendingPaymentsJob(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IScopeOverride scope,
    ISender sender,
    TimeProvider timeProvider,
    ILogger<PollPendingPaymentsJob> logger)
{
    private static readonly TimeSpan MinAge = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (tenantId, businessId) in await payments.ListBusinessesWithPendingAsync(now - MinAge, cancellationToken))
        {
            try
            {
                scope.UseScope(tenantId, businessId);
                await RunBusinessAsync(businessId, now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBusinessFailed(logger, ex, businessId);
            }
        }
    }

    private async Task RunBusinessAsync(Guid businessId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var payment in await payments.ListPendingAsync(cancellationToken))
        {
            var age = now - payment.CreatedAt;
            if (age < MinAge)
            {
                continue;
            }

            // A payment flagged for review (for example an amount mismatch) waits for the owner: polling it again would
            // only repeat the flag, and auto-failing it after 24 hours could hide money the client really paid.
            if (payment.ReviewReason is not null)
            {
                continue;
            }

            if (age > MaxAge)
            {
                var expired = new GatewayStatus(payment.Reference, null, payment.Amount, GatewayState.Failed);
                await sender.Send(new ConfirmOnlinePaymentCommand(payment.Id, expired), cancellationToken);
                continue;
            }

            if (string.IsNullOrWhiteSpace(payment.PollUrl))
            {
                continue;
            }

            var status = await gateway.CheckStatusAsync(businessId, payment.Currency, payment.PollUrl, cancellationToken);
            if (status.IsFailure)
            {
                LogStatusFailed(logger, payment.Id);
                continue;
            }

            var result = await sender.Send(new ConfirmOnlinePaymentCommand(payment.Id, status.Value), cancellationToken);
            if (result.IsSuccess && result.Value == ConfirmOutcome.Confirmed)
            {
                var receipt = await sender.Send(new SendReceiptCommand(payment.Id), cancellationToken);
                if (receipt.IsFailure)
                {
                    LogReceiptNotSent(logger, payment.Id);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment polling failed for business {BusinessId}")]
    private static partial void LogBusinessFailed(ILogger logger, Exception exception, Guid businessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow status check failed for payment {PaymentId}")]
    private static partial void LogStatusFailed(ILogger logger, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receipt for payment {PaymentId} could not be queued")]
    private static partial void LogReceiptNotSent(ILogger logger, Guid paymentId);
}
