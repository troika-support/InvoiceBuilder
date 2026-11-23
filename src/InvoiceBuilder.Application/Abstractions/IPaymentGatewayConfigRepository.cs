using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Application.Abstractions;

/// <summary>Paynow issues one integration per currency, so every lookup names the currency.</summary>
public interface IPaymentGatewayConfigRepository
{
    void Add(PaymentGatewayConfig config);

    /// <summary>Scoped by the current tenant and business query filters.</summary>
    Task<PaymentGatewayConfig?> GetAsync(Guid businessId, PaymentProvider provider, CurrencyCode currency, CancellationToken cancellationToken = default);

    /// <summary>Synchronous lookup for <c>IPaymentGateway.VerifyCallback</c>, which is synchronous in paynow.md section 2.</summary>
    PaymentGatewayConfig? Find(Guid businessId, PaymentProvider provider, CurrencyCode currency);

    /// <summary>
    /// Reviewed admin query: ignores query filters because the anonymous Paynow webhook has no tenant yet. Callers must
    /// then scope the rest of the request to the returned config's tenant and business via <see cref="IScopeOverride"/>.
    /// </summary>
    Task<PaymentGatewayConfig?> GetForWebhookAsync(Guid businessId, PaymentProvider provider, CurrencyCode currency, CancellationToken cancellationToken = default);
}

/// <summary>Scopes the current request to a tenant and business that were resolved without a signed-in user (webhooks).</summary>
public interface IScopeOverride
{
    void UseScope(Guid tenantId, Guid businessId);
}
