namespace InvoiceBuilder.Application.Abstractions;

/// <summary>Sends transactional email (password reset) through the configured provider.</summary>
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}
