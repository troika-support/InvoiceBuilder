using System.Net;
using System.Text.RegularExpressions;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;

namespace InvoiceBuilder.Infrastructure.Email;

/// <summary>Sends the simple transactional emails (password reset) through the configured provider.</summary>
public sealed partial class RichEmailSenderAdapter(IRichEmailSender inner) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        var html = UrlPattern().Replace(WebUtility.HtmlEncode(body), m => $"<a href=\"{m.Value}\">{m.Value}</a>")
            .Replace("\n", "<br>", StringComparison.Ordinal);
        return inner.SendAsync(new EmailMessage(recipient, subject, "<p>" + html + "</p>", body), cancellationToken);
    }

    [GeneratedRegex(@"https://[^\s<]+", RegexOptions.None, 1000)]
    private static partial Regex UrlPattern();
}
