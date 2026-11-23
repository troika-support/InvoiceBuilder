using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class PaymentGatewayConfigRepository(AppDbContext db) : IPaymentGatewayConfigRepository
{
    public void Add(PaymentGatewayConfig config) => db.PaymentGatewayConfigs.Add(config);

    public Task<PaymentGatewayConfig?> GetAsync(Guid businessId, PaymentProvider provider, CurrencyCode currency, CancellationToken cancellationToken = default) =>
        db.PaymentGatewayConfigs.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Provider == provider && c.Currency == currency, cancellationToken);

    public PaymentGatewayConfig? Find(Guid businessId, PaymentProvider provider, CurrencyCode currency)
    {
        return db.PaymentGatewayConfigs.FirstOrDefault(c => c.BusinessId == businessId && c.Provider == provider && c.Currency == currency);
    }

    // Reviewed admin query: the webhook has no tenant yet, so query filters are skipped for this one lookup.
    public Task<PaymentGatewayConfig?> GetForWebhookAsync(Guid businessId, PaymentProvider provider, CurrencyCode currency, CancellationToken cancellationToken = default)
    {
        return db.PaymentGatewayConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Provider == provider && c.Currency == currency, cancellationToken);
    }
}

/// <summary>Request-scoped tenant and business that a webhook sets after it has verified who the call is for.</summary>
public sealed class RequestScope : ICurrentTenant, ICurrentBusiness, IScopeOverride
{
    private Guid? _tenantId;

    public Guid TenantId => _tenantId ?? DevCurrentTenant.DevTenantId;

    /// <summary>The tenant a webhook set, or null when no override is active.</summary>
    public Guid? OverrideTenantId => _tenantId;

    public Guid? BusinessId { get; private set; }

    public void UseScope(Guid tenantId, Guid businessId)
    {
        _tenantId = tenantId;
        BusinessId = businessId;
    }
}
