using InvoiceBuilder.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Email;

/// <summary>Stub: records that an email would be sent, without its body (reset links are secrets). Replaced in prompt 21.</summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        LogStub(logger, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email stub: would send \"{Subject}\"")]
    private static partial void LogStub(ILogger logger, string subject);
}
