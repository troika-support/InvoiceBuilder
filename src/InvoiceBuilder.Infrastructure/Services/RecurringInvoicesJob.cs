using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>
/// Daily recurring invoices (R29, R31). Each occurrence is a draft copy of the source invoice; the draft and the
/// schedule's advanced NextRunDate commit in one transaction, so a second run the same day creates nothing.
/// </summary>
public sealed partial class RecurringInvoicesJob(
    IRecurringRepository schedules,
    IDocumentRepository documents,
    IBusinessRepository businesses,
    IScopeOverride scope,
    ISender sender,
    IUnitOfWork unitOfWork,
    IRichEmailSender email,
    InvoiceBuilder.Application.Notifications.INotificationRepository notifications,
    IOptions<AppOptions> app,
    TimeProvider timeProvider,
    ILogger<RecurringInvoicesJob> logger)
{
    private static readonly TimeSpan Harare = TimeSpan.FromHours(2);

    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime((timeProvider.GetUtcNow() + Harare).DateTime);
        foreach (var (tenantId, businessId) in await schedules.ListDueBusinessesAsync(today, cancellationToken))
        {
            try
            {
                scope.UseScope(tenantId, businessId);
                await RunBusinessAsync(businessId, today, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBusinessFailed(logger, ex, businessId);
            }
        }
    }

    private async Task RunBusinessAsync(Guid businessId, DateOnly today, CancellationToken cancellationToken)
    {
        var drafts = 0;
        foreach (var schedule in await schedules.ListDueAsync(today, cancellationToken))
        {
            var source = await documents.GetByIdAsync(schedule.SourceDocumentId, cancellationToken);
            if (source is null)
            {
                continue;
            }

            Guid? created = null;
            await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                var now = timeProvider.GetUtcNow();
                if (schedule.IsFinished)
                {
                    schedule.Pause(now);
                }
                else
                {
                    var copy = source.Duplicate(now, sourceDocumentId: source.Id);
                    documents.Add(copy);
                    schedule.RecordRun(now);
                    created = copy.Id;
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            if (created is not { } draftId)
            {
                continue;
            }

            if (schedule.AutoIssue && source.TemplateVersionId is { } versionId)
            {
                var issued = await sender.Send(new IssueDocumentCommand(draftId, versionId), cancellationToken);
                if (issued.IsFailure)
                {
                    LogIssueFailed(logger, draftId, issued.Error.Code);
                    drafts++;
                }
            }
            else
            {
                drafts++;
            }
        }

        if (drafts > 0)
        {
            await NotifyOwnerAsync(businessId, drafts, cancellationToken);
        }
    }

    private async Task NotifyOwnerAsync(Guid businessId, int count, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByIdAsync(businessId, cancellationToken);
        if (business is null)
        {
            return;
        }

        try
        {
            notifications.Add(InvoiceBuilder.Domain.Notifications.Notification.Create(business.TenantId, business.Id,
                InvoiceBuilder.Domain.Notifications.NotificationKind.RecurringDraftsReady,
                "Recurring drafts ready", count == 1 ? "1 recurring draft is ready to review." : $"{count} recurring drafts are ready to review.", "/recurring", timeProvider.GetUtcNow()));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(logger, ex, businessId);
        }

        if (string.IsNullOrWhiteSpace(business.Email))
        {
            return;
        }

        var link = $"{(app.Value.PublicBaseUrl ?? "https://localhost").TrimEnd('/')}/recurring";
        var content = EmailContent.RecurringDraftsReady(business.TradingName, count, link);
        try
        {
            await email.SendAsync(new EmailMessage(business.Email, content.Subject, content.Html, content.Text), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(logger, ex, businessId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Recurring invoices failed for business {BusinessId}")]
    private static partial void LogBusinessFailed(ILogger logger, Exception exception, Guid businessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recurring draft {InvoiceId} could not be auto-issued: {Code}")]
    private static partial void LogIssueFailed(ILogger logger, Guid invoiceId, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recurring drafts email failed for business {BusinessId}")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, Guid businessId);
}
