using System.Globalization;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Infrastructure.Payments.Paynow;

/// <summary>
/// Paynow over a typed HttpClient. Checked against developers.paynow.co.zw on 23 Nov 2026: "Initiate a transaction",
/// "Express Checkout Transactions", "Status Update", "Generating Hash", "Validating Hash" and "Test Mode".
/// </summary>
public sealed partial class PaynowGateway(
    HttpClient http,
    IPaymentGatewayConfigRepository configs,
    IBusinessSecretStore secrets,
    IOptions<AppOptions> appOptions,
    ILogger<PaynowGateway> logger) : IPaymentGateway
{
    // Web checkout (browser redirect) and express checkout (mobile money, no redirect).
    private const string InitiatePath = "interface/initiatetransaction";
    private const string MobilePath = "interface/remotetransaction";

    public Task<Result<PaymentInitiation>> InitiateWebAsync(PaymentRequest request, CancellationToken ct) =>
        InitiateAsync(request, InitiatePath, null, ct);

    public Task<Result<PaymentInitiation>> InitiateMobileAsync(PaymentRequest request, string phone, MobileMethod method, CancellationToken ct) =>
        InitiateAsync(request, MobilePath, (phone, method), ct);

    public async Task<Result<GatewayStatus>> CheckStatusAsync(Guid businessId, CurrencyCode currency, string pollUrl, CancellationToken ct)
    {
        var config = await configs.GetAsync(businessId, PaymentProvider.Paynow, currency, ct);
        if (config is null)
        {
            return Result.Failure<GatewayStatus>(NotConfigured);
        }

        var key = secrets.Unprotect(config.IntegrationKeyEncrypted);
        var response = await PostAsync(pollUrl, [], ct);
        if (response.IsFailure)
        {
            return Result.Failure<GatewayStatus>(response.Error);
        }

        var fields = response.Value;
        if (!PaynowHash.Verify(fields, key))
        {
            LogBadStatusHash(businessId);
            return Result.Failure<GatewayStatus>(new Error("Paynow.BadHash", "Paynow's status response could not be verified."));
        }

        var status = PaynowStatusMapper.Map(Get(fields, "status"));
        if (!decimal.TryParse(Get(fields, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return Result.Failure<GatewayStatus>(new Error("Paynow.BadAmount", "Paynow's status response had no valid amount."));
        }

        return Result.Success(new GatewayStatus(
            Get(fields, "reference") ?? string.Empty,
            Get(fields, "paynowreference"),
            amount,
            status.State,
            status.FlagForReview));
    }

    public bool VerifyCallback(Guid businessId, CurrencyCode currency, IReadOnlyDictionary<string, string> fields)
    {
        var config = configs.Find(businessId, PaymentProvider.Paynow, currency);
        return config is not null && PaynowHash.Verify(fields, secrets.Unprotect(config.IntegrationKeyEncrypted));
    }

    private static Error NotConfigured { get; } = new("Paynow.NotConfigured", "Online payments are not set up for this business.");

    /// <summary>
    /// Paynow posts status updates here. The currency is in the path because each currency has its own integration
    /// key, and the callback hash can only be checked with the key of the integration that started the payment.
    /// </summary>
    public static string ResultPath(Guid businessId, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return $"/webhooks/paynow/{businessId}/{currency.Code.ToLowerInvariant()}";
    }

    private static string? Get(IReadOnlyDictionary<string, string> fields, string name) =>
        fields.FirstOrDefault(f => f.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private async Task<Result<PaymentInitiation>> InitiateAsync(
        PaymentRequest request,
        string path,
        (string Phone, MobileMethod Method)? mobile,
        CancellationToken ct)
    {
        // Paynow issues a separate integration per currency, so the invoice's currency picks the integration.
        var currency = CurrencyCode.Create(request.Currency);
        var config = currency.IsSuccess
            ? await configs.GetAsync(request.BusinessId, PaymentProvider.Paynow, currency.Value, ct)
            : null;
        if (config is null)
        {
            return Result.Failure<PaymentInitiation>(NotConfigured);
        }

        var baseUrl = (appOptions.Value.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var key = secrets.Unprotect(config.IntegrationKeyEncrypted);

        // The hash covers the values in the order they are sent, so the form is hashed exactly as it is posted.
        var form = new Dictionary<string, string>
        {
            ["id"] = config.IntegrationId,
            ["reference"] = request.Reference,
            ["amount"] = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["additionalinfo"] = request.Description,
            ["returnurl"] = request.ClientToken is { Length: > 0 } clientToken
                ? $"{baseUrl}/d/{Uri.EscapeDataString(clientToken)}/paid?payment={request.PaymentId}"
                : $"{baseUrl}/d/{request.PaymentId}/paid",
            ["resulturl"] = baseUrl + ResultPath(request.BusinessId, config.Currency),
        };
        if (AuthEmail(request, config.IsLive, mobile is not null) is { } authEmail)
        {
            form["authemail"] = authEmail;
        }

        // Unique per request (max 32 chars): lets Paynow recognise a resent request instead of debiting twice.
        form["merchanttrace"] = request.PaymentId.ToString("N");
        if (mobile is { } m)
        {
            form["phone"] = m.Phone;
            form["method"] = m.Method switch
            {
                MobileMethod.OneMoney => "onemoney",
                MobileMethod.InnBucks => "innbucks",
                _ => "ecocash",
            };
        }

        form["status"] = "Message";
        form["hash"] = PaynowHash.Compute(form, key);

        var response = await PostAsync(path, form, ct);
        if (response.IsFailure)
        {
            return Result.Failure<PaymentInitiation>(response.Error);
        }

        var fields = response.Value;
        if (!string.Equals(Get(fields, "status"), "Ok", StringComparison.OrdinalIgnoreCase))
        {
            var error = Get(fields, "error");
            LogInitiateRejected(request.Reference, Get(fields, "status"), error);
            return Result.Failure<PaymentInitiation>(new Error(
                "Paynow.Rejected",
                error is { Length: > 0 } && error.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
                    ? "The wallet doesn't have enough funds. Top up and try again, or choose another method."
                    : "Paynow did not accept the payment. Please try again."));
        }

        if (!PaynowHash.Verify(fields, key))
        {
            LogBadStatusHash(request.BusinessId);
            return Result.Failure<PaymentInitiation>(new Error("Paynow.BadHash", "Paynow's response could not be verified."));
        }

        var pollUrl = Get(fields, "pollurl");
        return string.IsNullOrWhiteSpace(pollUrl)
            ? Result.Failure<PaymentInitiation>(new Error("Paynow.NoPollUrl", "Paynow did not return a status link."))
            : Result.Success(new PaymentInitiation(
                Get(fields, "browserurl"),
                pollUrl,
                Get(fields, "instructions"),
                Get(fields, "authorizationcode"),
                Get(fields, "authorizationexpires")));
    }

    /// <summary>
    /// Paynow's authemail rules: in test mode it must be the merchant's Paynow login email or the test can't complete;
    /// express checkout (mobile money) requires one; on web checkout a registered address prompts a Paynow login, so
    /// only the client's own email is sent there.
    /// </summary>
    public static string? AuthEmail(PaymentRequest request, bool isLive, bool isMobile)
    {
        var payer = string.IsNullOrWhiteSpace(request.PayerEmail) ? null : request.PayerEmail.Trim();
        var merchant = string.IsNullOrWhiteSpace(request.MerchantEmail) ? null : request.MerchantEmail.Trim();
        if (!isLive)
        {
            return merchant ?? payer;
        }

        return isMobile ? payer ?? merchant : payer;
    }

    private async Task<Result<Dictionary<string, string>>> PostAsync(string path, Dictionary<string, string> form, CancellationToken ct)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(path, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                LogHttpFailure((int)response.StatusCode);
                return Result.Failure<Dictionary<string, string>>(new Error("Paynow.Unavailable", "Paynow is not available right now."));
            }

            return Result.Success(ParseForm(body));
        }
        catch (HttpRequestException ex)
        {
            LogHttpException(ex);
            return Result.Failure<Dictionary<string, string>>(new Error("Paynow.Unavailable", "Paynow is not available right now."));
        }
    }

    /// <summary>Parses a url-encoded body keeping the order received (the hash depends on it).</summary>
    public static Dictionary<string, string> ParseForm(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=', StringComparison.Ordinal);
            var name = Uri.UnescapeDataString((index < 0 ? pair : pair[..index]).Replace('+', ' '));
            var value = index < 0 ? string.Empty : Uri.UnescapeDataString(pair[(index + 1)..].Replace('+', ' '));
            fields[name] = value;
        }

        return fields;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow response hash did not verify for business {BusinessId}")]
    private partial void LogBadStatusHash(Guid businessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow rejected {Reference} with status {Status}: {PaynowError}")]
    private partial void LogInitiateRejected(string reference, string? status, string? paynowError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow returned HTTP {StatusCode}")]
    private partial void LogHttpFailure(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow request failed")]
    private partial void LogHttpException(Exception exception);
}
