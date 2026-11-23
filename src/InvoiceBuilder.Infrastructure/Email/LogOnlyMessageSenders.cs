using InvoiceBuilder.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Email;

/// <summary>Development email sender: logs that a message would be sent, never its body or address (links are secrets).</summary>
public sealed partial class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IRichEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogSend(logger, message.Subject, message.Attachment is not null);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (log only): would send \"{Subject}\" (attachment: {HasAttachment})")]
    private static partial void LogSend(ILogger logger, string subject, bool hasAttachment);
}

public sealed partial class LogOnlyWhatsAppSender(ILogger<LogOnlyWhatsAppSender> logger) : IWhatsAppSender
{
    public Task<InvoiceBuilder.Domain.Common.Result<string>> SendTemplateAsync(WhatsAppTemplateMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogSend(logger, message.TemplateName, message.Parameters.Count);
        return Task.FromResult(InvoiceBuilder.Domain.Common.Result.Success($"log-{Guid.NewGuid():N}"));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "WhatsApp (log only): would send template {Template} with {ParameterCount} parameters")]
    private static partial void LogSend(ILogger logger, string template, int parameterCount);
}

/// <summary>Used outside Development when WhatsApp is not configured: every send fails, so the message shows as failed instead of sent.</summary>
public sealed class DisabledWhatsAppSender : IWhatsAppSender
{
    public Task<InvoiceBuilder.Domain.Common.Result<string>> SendTemplateAsync(WhatsAppTemplateMessage message, CancellationToken cancellationToken = default) =>
        Task.FromResult(InvoiceBuilder.Domain.Common.Result.Failure<string>(
            new InvoiceBuilder.Domain.Common.Error("WhatsApp.NotConfigured", "WhatsApp is not set up for this installation.")));
}
