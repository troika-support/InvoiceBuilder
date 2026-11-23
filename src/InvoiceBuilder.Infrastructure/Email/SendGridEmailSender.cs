using System.Net.Http.Headers;
using System.Net.Http.Json;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Email;

/// <summary>SendGrid v3 mail send over a typed HttpClient. VERIFY: endpoint and payload shape against SendGrid docs.</summary>
public sealed class SendGridEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender, IRichEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default) =>
        SendAsync(new EmailMessage(recipient, subject, System.Net.WebUtility.HtmlEncode(body), body), cancellationToken);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.From))
        {
            throw new InvalidOperationException("Email:ApiKey and Email:From must be set for the SendGrid provider.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["personalizations"] = new[] { new { to = new[] { new { email = message.To } } } },
            ["from"] = new { email = o.From, name = string.IsNullOrWhiteSpace(message.FromName) ? o.FromName : message.FromName },
            ["subject"] = message.Subject,
            ["content"] = new[]
            {
                new { type = "text/plain", value = message.Text },
                new { type = "text/html", value = message.Html },
            },
        };
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            payload["reply_to"] = new { email = message.ReplyTo };
        }

        if (message.Attachment is { } a)
        {
            payload["attachments"] = new[] { new { content = Convert.ToBase64String(a.Content), type = a.ContentType, filename = a.FileName, disposition = "attachment" } };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            if (code == 429 || code >= 500)
            {
                throw new MessageProviderUnavailableException($"The email provider is unavailable right now ({code}).");
            }

            throw new InvalidOperationException($"Email provider rejected the message ({code}).");
        }
    }
}

/// <summary>The provider is down or throttling (HTTP 429 / 5xx). Nothing was accepted, so the send can be retried later.</summary>
public sealed class MessageProviderUnavailableException : Exception
{
    public MessageProviderUnavailableException()
    {
    }

    public MessageProviderUnavailableException(string message)
        : base(message)
    {
    }

    public MessageProviderUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
