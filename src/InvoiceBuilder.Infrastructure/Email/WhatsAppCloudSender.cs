using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Common;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Email;

/// <summary>WhatsApp Business Cloud API template sender. VERIFY: Graph API version, payload and response shape against Meta docs.</summary>
public sealed class WhatsAppCloudSender(HttpClient http, IOptions<WhatsAppOptions> options) : IWhatsAppSender
{
    /// <summary>Error code for a temporary outage (HTTP 429 / 5xx); the send job retries these.</summary>
    public const string UnavailableCode = "WhatsApp.Unavailable";

    public async Task<Result<string>> SendTemplateAsync(WhatsAppTemplateMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var o = options.Value;
        var token = string.IsNullOrWhiteSpace(message.AccessToken) ? o.AccessToken : message.AccessToken;
        var phoneNumberId = string.IsNullOrWhiteSpace(message.PhoneNumberId) ? o.PhoneNumberId : message.PhoneNumberId;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return Result.Failure<string>(new Error("WhatsApp.NotConfigured", "WhatsApp is not set up."));
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = new string(message.To.Where(char.IsDigit).ToArray()),
            type = "template",
            template = new
            {
                name = message.TemplateName,
                language = new { code = o.TemplateLanguage },
                components = new[]
                {
                    new { type = "body", parameters = message.Parameters.Select(p => new { type = "text", text = p }).ToArray() },
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.GraphApiVersion}/{phoneNumberId}/messages") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            return code == 429 || code >= 500
                ? Result.Failure<string>(new Error(UnavailableCode, $"WhatsApp is unavailable right now ({code})."))
                : Result.Failure<string>(new Error("WhatsApp.Rejected", $"WhatsApp rejected the message ({code})."));
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var id = doc.RootElement.TryGetProperty("messages", out var m) && m.GetArrayLength() > 0 ? m[0].GetProperty("id").GetString() : null;
        return id is null
            ? Result.Failure<string>(new Error("WhatsApp.NoId", "WhatsApp did not return a message id."))
            : Result.Success(id);
    }
}
