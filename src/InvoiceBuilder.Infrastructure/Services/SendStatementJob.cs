using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Application.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Services;

public sealed class HangfireStatementJobScheduler(IBackgroundJobClient jobs, ICurrentTenant tenant, ICurrentBusiness business) : IStatementJobScheduler
{
    public void Enqueue(Guid clientId, DateOnly periodStart, DateOnly periodEnd, string email, string? message)
    {
        if (business.BusinessId is not { } businessId)
        {
            throw new InvalidOperationException("A business must be selected to send a statement.");
        }

        var tenantId = tenant.TenantId;
        var fromText = periodStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var toText = periodEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        jobs.Enqueue<SendStatementJob>(j => j.RunAsync(tenantId, businessId, clientId, fromText, toText, email, message, CancellationToken.None));
    }
}

/// <summary>Renders the client statement PDF and emails it (spec §4.2 "Send statement"). The outcome goes on the client's activity.</summary>
public sealed partial class SendStatementJob(
    IScopeOverride scope,
    ISender sender,
    IRichEmailSender email,
    IBusinessSettingsRepository settings,
    IActivityLog activity,
    IUnitOfWork unitOfWork,
    ILogger<SendStatementJob> logger)
{
    // Not retried automatically: an error after the provider accepted the email would send it twice.
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(Guid tenantId, Guid businessId, Guid clientId, string from, string to, string recipient, string? message, CancellationToken cancellationToken)
    {
        scope.UseScope(tenantId, businessId);
        var fromDate = DateOnly.ParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var toDate = DateOnly.ParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var period = SendClientStatementHandler.Period(fromDate, toDate);
        try
        {
            var statement = await sender.Send(new GetClientStatementQuery(clientId, fromDate, toDate), cancellationToken);
            var file = await sender.Send(new GetClientStatementFileQuery(clientId, fromDate, toDate), cancellationToken);
            if (statement.IsFailure || file.IsFailure)
            {
                await RecordAsync(clientId, "StatementFailed", $"Statement for {period} could not be created: {(statement.IsFailure ? statement.Error.Message : file.Error.Message)}", cancellationToken);
                return;
            }

            var biz = await settings.GetAsync(businessId, cancellationToken);
            var content = EmailContent.Statement(statement.Value.Client.Name, statement.Value.Business.TradingName, period, message);
            await email.SendAsync(
                new EmailMessage(
                    recipient,
                    content.Subject,
                    content.Html,
                    content.Text,
                    new EmailAttachment(file.Value.FileName, file.Value.ContentType, file.Value.Content),
                    string.IsNullOrWhiteSpace(biz?.EmailFromName) ? null : biz.EmailFromName,
                    biz?.EmailReplyTo),
                cancellationToken);
            await RecordAsync(clientId, "StatementSent", $"Statement for {period} emailed to {recipient}.", cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, ex, clientId);
            await RecordAsync(clientId, "StatementFailed", $"Statement for {period} could not be emailed to {recipient}: {ex.Message} Send it again from the client panel.", cancellationToken);
        }
    }

    private async Task RecordAsync(Guid clientId, string action, string summary, CancellationToken cancellationToken)
    {
        await activity.AddAsync("Client", clientId, action, summary, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Statement email for client {ClientId} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid clientId);
}
