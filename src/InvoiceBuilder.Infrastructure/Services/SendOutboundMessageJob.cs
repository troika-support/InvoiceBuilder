using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Services;

public sealed class HangfireMessageJobScheduler(IBackgroundJobClient jobs, ICurrentTenant tenant, ICurrentBusiness business) : IMessageJobScheduler
{
    public void Enqueue(Guid messageId, string link, string? userMessage)
    {
        if (business.BusinessId is not { } businessId)
        {
            throw new InvalidOperationException("A business must be selected to send a document.");
        }

        var tenantId = tenant.TenantId;
        jobs.Enqueue<SendOutboundMessageJob>(j => j.RunAsync(messageId, tenantId, businessId, link, userMessage, CancellationToken.None));
    }
}

/// <summary>Sends one queued message and records the outcome (R28). Failures are stored on the message and shown on the document.</summary>
public sealed partial class SendOutboundMessageJob(
    IScopeOverride scope,
    IOutboundMessageRepository messages,
    IDocumentRepository documents,
    IClientRepository clients,
    IBusinessSettingsRepository settings,
    InvoiceBuilder.Application.Payments.IBusinessSecretStore secrets,
    IFileStore files,
    IUnitOfWork unitOfWork,
    IActivityLog activity,
    IRichEmailSender email,
    IWhatsAppSender whatsApp,
    IDisplayFormatter format,
    IOptions<WhatsAppOptions> whatsAppOptions,
    InvoiceBuilder.Application.Documents.ClientLinkService links,
    IBackgroundJobClient retries,
    TimeProvider timeProvider,
    ILogger<SendOutboundMessageJob> logger)
{
    /// <summary>Waits before each retry of a temporary provider outage; after the last one the message is marked failed.</summary>
    public static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

    /// <summary>Entry point for jobs queued before retries existed; same as attempt 0.</summary>
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(Guid messageId, Guid tenantId, Guid businessId, string link, string? userMessage, CancellationToken cancellationToken) =>
        RunAttemptAsync(messageId, tenantId, businessId, link, userMessage, 0, cancellationToken);

    /// <summary>
    /// Sends the message. A temporary outage (HTTP 429/5xx, network error, timeout) leaves it queued and schedules
    /// another attempt; the provider accepted nothing in those cases, so a retry can't send twice. Hangfire's own
    /// retry stays off for the same reason: an error after the provider accepted the message must not resend it.
    /// </summary>
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAttemptAsync(Guid messageId, Guid tenantId, Guid businessId, string link, string? userMessage, int attempt, CancellationToken cancellationToken)
    {
        scope.UseScope(tenantId, businessId);
        var message = await messages.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.Status != MessageStatus.Queued || message.DocumentId is not { } documentId)
        {
            return;
        }

        var document = await documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null || document.Number is null)
        {
            message.MarkFailed("The document could not be found.", timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(link))
        {
            // New jobs carry no link (Hangfire keeps arguments in plain text); read it from the document instead.
            if (links.UrlFor(document) is not { } resolved)
            {
                message.MarkFailed("The client link could not be found. Send the document again.", timeProvider.GetUtcNow());
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            link = resolved;
        }

        var client = await clients.GetByIdAsync(document.ClientId, cancellationToken);
        var name = client?.Name ?? "there";
        var number = document.Number.Value.ToString();
        var amount = format.Money(document.Total, document.Currency.ToString());
        var businessSettings = await settings.GetAsync(businessId, cancellationToken);
        var fromName = string.IsNullOrWhiteSpace(businessSettings?.EmailFromName) ? null : businessSettings.EmailFromName;

        try
        {
            if (message.Channel == MessageChannel.Email)
            {
                var content = message.TemplateName switch
                {
                    MessageTemplates.ReminderEmail => EmailContent.ReminderNote(name, number, userMessage ?? amount, link, false),
                    MessageTemplates.InstalmentReminderEmail => EmailContent.ReminderNote(name, number, userMessage ?? amount, link, true),
                    MessageTemplates.PlanRescheduledEmail => EmailContent.PlanRescheduled(name, number, link),
                    _ when document.Type == Domain.Documents.DocumentType.Receipt => EmailContent.Receipt(name, number, amount, link),
                    _ => EmailContent.Document(name, number, amount, link, userMessage),
                };
                var attachment = await LoadPdfAsync(document, number, cancellationToken);
                await email.SendAsync(new EmailMessage(message.To, content.Subject, content.Html, content.Text, attachment, fromName, businessSettings?.EmailReplyTo), cancellationToken);
                message.MarkSent(null, timeProvider.GetUtcNow());
            }
            else
            {
                var o = whatsAppOptions.Value;
                var token = businessSettings is { WhatsAppTokenEncrypted.Length: > 0 } ? secrets.Unprotect(businessSettings.WhatsAppTokenEncrypted) : null;
                var template = document.Type == Domain.Documents.DocumentType.Receipt ? o.PaymentReceiptTemplate : o.DocumentLinkTemplate;
                var result = await whatsApp.SendTemplateAsync(new WhatsAppTemplateMessage(message.To, template, [name, number, amount, link], businessSettings?.WhatsAppPhoneNumberId, token), cancellationToken);
                if (result.IsFailure && result.Error.Code == Email.WhatsAppCloudSender.UnavailableCode && attempt < RetryDelays.Length)
                {
                    await ScheduleRetryAsync(message, document, number, tenantId, businessId, userMessage, attempt, result.Error.Message, cancellationToken);
                    return;
                }

                if (result.IsFailure)
                {
                    message.MarkFailed(result.Error.Message, timeProvider.GetUtcNow());
                    await activity.AddAsync(nameof(Domain.Documents.Document), document.Id, "SendFailed", $"{number} could not be sent by WhatsApp: {result.Error.Message}", cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return;
                }

                message.MarkSent(result.Value, timeProvider.GetUtcNow());
            }

            document.MarkSent(timeProvider.GetUtcNow());
            await activity.AddAsync(nameof(Domain.Documents.Document), document.Id, "Sent", $"{number} sent by {message.Channel}.", cancellationToken);
        }
        catch (Exception ex) when (IsTemporary(ex, cancellationToken) && attempt < RetryDelays.Length)
        {
            await ScheduleRetryAsync(message, document, number, tenantId, businessId, userMessage, attempt, ex.Message, cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, message.Id, message.Channel);
            message.MarkFailed(ex.Message, timeProvider.GetUtcNow());
            await activity.AddAsync(nameof(Domain.Documents.Document), document.Id, "SendFailed", $"{number} could not be sent by {message.Channel}: {ex.Message}", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Outages where the provider accepted nothing: throttling/5xx, network failures and timeouts.</summary>
    public static bool IsTemporary(Exception ex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex switch
        {
            Email.MessageProviderUnavailableException => true,
            HttpRequestException => true,
            TimeoutException => true,
            TaskCanceledException => !cancellationToken.IsCancellationRequested,
            _ => ex.GetType().Name is "TimeoutRejectedException" or "BrokenCircuitException",
        };
    }

    private async Task ScheduleRetryAsync(OutboundMessage message, Domain.Documents.Document document, string number, Guid tenantId, Guid businessId, string? userMessage, int attempt, string reason, CancellationToken cancellationToken)
    {
        var delay = RetryDelays[attempt];
        LogRetry(logger, message.Id, message.Channel, attempt + 1, delay);
        await activity.AddAsync(nameof(Domain.Documents.Document), document.Id, "SendRetrying", $"{number} could not be sent by {message.Channel} yet ({reason}); trying again in {(delay.TotalHours >= 1 ? $"{delay.TotalHours:0} h" : $"{delay.TotalMinutes:0} min")}.", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var messageId = message.Id;
        var next = attempt + 1;
        retries.Schedule<SendOutboundMessageJob>(j => j.RunAttemptAsync(messageId, tenantId, businessId, string.Empty, userMessage, next, CancellationToken.None), delay);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbound message {MessageId} on {Channel}: provider unavailable, retry {Retry} in {Delay}.")]
    private static partial void LogRetry(ILogger logger, Guid messageId, MessageChannel channel, int retry, TimeSpan delay);

    private async Task<EmailAttachment?> LoadPdfAsync(Domain.Documents.Document document, string number, CancellationToken cancellationToken)
    {
        if (document.TemplateVersionId is not { } versionId)
        {
            return null;
        }

        await using var stream = await files.OpenAsync(PdfKeys.For(document.Id, versionId), cancellationToken);
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return new EmailAttachment($"{number}.pdf", "application/pdf", buffer.ToArray());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbound message {MessageId} on {Channel} failed.")]
    private static partial void LogFailed(ILogger logger, Guid messageId, MessageChannel channel);
}
