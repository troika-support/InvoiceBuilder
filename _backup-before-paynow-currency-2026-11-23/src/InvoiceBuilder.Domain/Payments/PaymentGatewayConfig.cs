using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Payments;

public enum PaymentProvider
{
    Paynow = 0,
}

/// <summary>
/// One business's gateway integration. The key is stored encrypted (Data Protection) and never exposed.
/// ResultUrl and ReturnUrl are computed from the public base URL, not stored.
/// </summary>
public sealed class PaymentGatewayConfig : BusinessEntity
{
    private PaymentGatewayConfig()
    {
    }

    public PaymentProvider Provider { get; private set; }

    public string IntegrationId { get; private set; } = string.Empty;

    public string IntegrationKeyEncrypted { get; private set; } = string.Empty;

    public bool IsLive { get; private set; }

    public static Result<PaymentGatewayConfig> Create(
        Guid tenantId,
        Guid businessId,
        PaymentProvider provider,
        string integrationId,
        string integrationKeyEncrypted,
        bool isLive,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(integrationId))
        {
            return Result.Failure<PaymentGatewayConfig>(new Error("PaymentGateway.IntegrationIdRequired", "An integration ID is required."));
        }

        if (string.IsNullOrWhiteSpace(integrationKeyEncrypted))
        {
            return Result.Failure<PaymentGatewayConfig>(new Error("PaymentGateway.IntegrationKeyRequired", "An integration key is required."));
        }

        var config = new PaymentGatewayConfig
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Provider = provider,
            IntegrationId = integrationId.Trim(),
            IntegrationKeyEncrypted = integrationKeyEncrypted,
            IsLive = isLive,
        };
        config.Initialise(now);
        return Result.Success(config);
    }

    public void Update(string integrationId, string? newIntegrationKeyEncrypted, bool isLive, DateTimeOffset now)
    {
        IntegrationId = integrationId.Trim();
        if (!string.IsNullOrWhiteSpace(newIntegrationKeyEncrypted))
        {
            IntegrationKeyEncrypted = newIntegrationKeyEncrypted;
        }

        IsLive = isLive;
        Touch(now);
    }
}
