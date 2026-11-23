using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Application.Documents;

/// <summary>Encrypts client link tokens at rest so the same link can be sent again (R26). Implemented with Data Protection.</summary>
public interface IClientLinkTokenProtector
{
    string Protect(string token);

    /// <summary>Null when the value is missing or can no longer be decrypted.</summary>
    string? Unprotect(string? protectedToken);
}

/// <summary>
/// One place that knows a document's client link. A document keeps the same link for its whole life, so the PDF's QR
/// code, the first email, every reminder and a pending Paynow return all keep working. A new link is only made when
/// none can be recovered (documents issued before links were stored).
/// </summary>
public sealed class ClientLinkService(IClientLinkTokenProtector protector, IOptions<AppOptions> app)
{
    /// <summary>The document's current token, or null if it can't be recovered.</summary>
    public string? CurrentToken(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var token = protector.Unprotect(document.LinkTokenProtected);
        return token is not null && ClientLinkToken.Hash(token) == document.LinkTokenHash ? token : null;
    }

    /// <summary>Returns <paramref name="known"/> or the stored token; otherwise makes, stores and returns a new one.</summary>
    public Result<string> EnsureToken(Document document, string? known, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.IsNullOrEmpty(known) && ClientLinkToken.Hash(known) == document.LinkTokenHash)
        {
            if (document.LinkTokenProtected is null)
            {
                var stored = document.RotateLinkToken(document.LinkTokenHash!, now, protector.Protect(known));
                if (stored.IsFailure)
                {
                    return Result.Failure<string>(stored.Error);
                }
            }

            return Result.Success(known);
        }

        if (CurrentToken(document) is { } current)
        {
            return Result.Success(current);
        }

        var token = ClientLinkToken.Generate();
        var rotated = document.RotateLinkToken(ClientLinkToken.Hash(token), now, protector.Protect(token));
        return rotated.IsSuccess ? Result.Success(token) : Result.Failure<string>(rotated.Error);
    }

    /// <summary>Makes a new link on purpose; every earlier link stops working.</summary>
    public Result<string> Revoke(Document document, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);
        var token = ClientLinkToken.Generate();
        var rotated = document.RotateLinkToken(ClientLinkToken.Hash(token), now, protector.Protect(token));
        return rotated.IsSuccess ? Result.Success(token) : Result.Failure<string>(rotated.Error);
    }

    public string Url(string token) => $"{BaseUrl}/d/{Uri.EscapeDataString(token)}";

    /// <summary>The public link for a document, or null if none can be recovered.</summary>
    public string? UrlFor(Document document) => CurrentToken(document) is { } token ? Url(token) : null;

    private string BaseUrl => (app.Value.PublicBaseUrl ?? "https://localhost").TrimEnd('/');
}
