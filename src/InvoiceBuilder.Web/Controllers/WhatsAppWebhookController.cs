using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>
/// WhatsApp Cloud API delivery status callback (spec 4.3). Anonymous, rate limited. The body is only used after the
/// X-Hub-Signature-256 HMAC is verified with the app secret. VERIFY: payload shape against Meta docs.
/// </summary>
[ApiController]
[Route("webhooks/whatsapp")]
public sealed partial class WhatsAppWebhookController(
    IOptions<WhatsAppOptions> options,
    IOutboundMessageRepository messages,
    IScopeOverride scope,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    public const string RateLimitPolicy = "whatsapp-webhook";

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicy)]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = options.Value.VerifyToken;
        if (mode == "subscribe" && !string.IsNullOrEmpty(expected) && token is not null && FixedTimeEquals(token, expected))
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Forbid();
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        if (!IsSignatureValid(body, Request.Headers["X-Hub-Signature-256"].ToString(), options.Value.AppSecret))
        {
            LogRejected();
            return Unauthorized();
        }

        using var doc = JsonDocument.Parse(body);
        foreach (var status in EnumerateStatuses(doc.RootElement))
        {
            var message = await messages.GetByProviderIdForWebhookAsync(MessageChannel.WhatsApp, status.Id, cancellationToken);
            if (message is null)
            {
                continue;
            }

            scope.UseScope(message.TenantId, message.BusinessId);
            message.ApplyDelivery(status.Status, status.Error, timeProvider.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    public static bool IsSignatureValid(byte[] body, string? header, string? appSecret)
    {
        ArgumentNullException.ThrowIfNull(body);
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(appSecret) || header is null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(header[prefix.Length..]));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static IEnumerable<(string Id, MessageStatus Status, string? Error)> EnumerateStatuses(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value) || !value.TryGetProperty("statuses", out var statuses) || statuses.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var s in statuses.EnumerateArray())
                {
                    var id = s.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var state = s.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
                    MessageStatus? mapped = state switch
                    {
                        "sent" => MessageStatus.Sent,
                        "delivered" => MessageStatus.Delivered,
                        "read" => MessageStatus.Read,
                        "failed" => MessageStatus.Failed,
                        _ => null,
                    };
                    if (id is null || mapped is null)
                    {
                        continue;
                    }

                    string? error = null;
                    if (s.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
                    {
                        error = errs[0].TryGetProperty("title", out var t) ? t.GetString() : null;
                    }

                    yield return (id, mapped.Value, error);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "WhatsApp webhook rejected: bad signature.")]
    private partial void LogRejected();
}
