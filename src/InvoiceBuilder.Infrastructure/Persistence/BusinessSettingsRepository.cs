using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class BusinessSettingsRepository(AppDbContext db) : IBusinessSettingsRepository
{
    public void Add(BusinessSettings settings) => db.BusinessSettings.Add(settings);

    public Task<BusinessSettings?> GetAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken);
}
