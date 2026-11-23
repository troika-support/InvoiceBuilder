using InvoiceBuilder.Domain.Businesses;

namespace InvoiceBuilder.Application.Abstractions;

public interface IBusinessSettingsRepository
{
    void Add(BusinessSettings settings);

    /// <summary>Scoped by the current tenant and business query filters; null until first saved.</summary>
    Task<BusinessSettings?> GetAsync(Guid businessId, CancellationToken cancellationToken = default);
}
