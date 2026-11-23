using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Application.Payments;

public interface IPaymentGateway
{
    Task<Result<PaymentInitiation>> InitiateWebAsync(PaymentRequest request, CancellationToken ct);

    Task<Result<PaymentInitiation>> InitiateMobileAsync(PaymentRequest request, string phone, MobileMethod method, CancellationToken ct);

    Task<Result<GatewayStatus>> CheckStatusAsync(Guid businessId, string pollUrl, CancellationToken ct);

    bool VerifyCallback(Guid businessId, IReadOnlyDictionary<string, string> fields);
}

/// <param name="PayerEmail">The client's email, or empty when unknown.</param>
/// <param name="MerchantEmail">The business's email. Paynow test mode only completes when authemail is the merchant's
/// Paynow login email, and express checkout needs some email when the client has none.</param>
public sealed record PaymentRequest(
    Guid BusinessId,
    Guid PaymentId,
    string Reference,
    decimal Amount,
    string Currency,
    string Description,
    string PayerEmail,
    string? ClientToken = null,
    string? MerchantEmail = null);

/// <summary>
/// Result of starting a payment. Web payments carry a <see cref="RedirectUrl"/>; mobile money carries
/// <see cref="Instructions"/>; InnBucks has no phone prompt and instead returns an authorisation code the client
/// enters (or opens via <see cref="AuthorizationDeepLink"/>) in the InnBucks app before <see cref="AuthorizationExpires"/>.
/// </summary>
public sealed record PaymentInitiation(
    string? RedirectUrl,
    string PollUrl,
    string? Instructions,
    string? AuthorizationCode = null,
    string? AuthorizationExpires = null)
{
    public string? AuthorizationDeepLink => AuthorizationCode is { Length: > 0 } code
        ? "com.innbucks.customer://purchase?paymentToken=" + Uri.EscapeDataString(code)
        : null;
}

// NeedsReview is an addition to paynow.md section 2: it carries the "flag Disputed/Refunded for review" rule of section 5.
public sealed record GatewayStatus(string Reference, string? ProviderReference, decimal Amount, GatewayState State, bool NeedsReview = false);

public enum GatewayState
{
    Pending,
    Paid,
    Cancelled,
    Failed,
}

public enum MobileMethod
{
    EcoCash,
    OneMoney,
    InnBucks,
}

/// <summary>Encrypts and decrypts per-business secrets (Data Protection). Never log or return the plain value.</summary>
public interface IBusinessSecretStore
{
    string Protect(string plainText);

    string Unprotect(string protectedValue);
}
