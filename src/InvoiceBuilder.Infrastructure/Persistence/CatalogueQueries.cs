using InvoiceBuilder.Application.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class CatalogueQueries(AppDbContext db) : ICatalogueQueries
{
    public async Task<IReadOnlyList<CatalogueItemRow>> ListAsync(string? search, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = db.CatalogueItems.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        var items = await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var ids = items.Select(i => i.Id).ToList();
        var used = await db.DocumentLines
            .Where(l => l.CatalogueItemId != null && ids.Contains(l.CatalogueItemId.Value))
            .Select(l => l.CatalogueItemId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var usedSet = used.ToHashSet();
        var taxNames = await db.TaxRates.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return items.Select(i => new CatalogueItemRow(
                i.Id,
                i.Name,
                i.Description,
                i.Unit,
                i.UnitPrice,
                i.Currency.Code,
                i.TaxRateId,
                i.TaxRateId is { } t && taxNames.TryGetValue(t, out var name) ? name : null,
                i.HsCode?.Value,
                i.IsRecurring,
                i.IsArchived,
                usedSet.Contains(i.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogueSearchHit>> SearchAsync(string term, int take, CancellationToken cancellationToken = default)
    {
        var query = db.CatalogueItems.AsNoTracking().Where(c => !c.IsArchived);
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        var items = await query.OrderBy(c => c.Name).Take(take).ToListAsync(cancellationToken);
        return items.Select(i => new CatalogueSearchHit(i.Id, i.Name, i.Description, i.Unit, i.UnitPrice, i.Currency.Code, i.TaxRateId, i.HsCode?.Value)).ToList();
    }

    public Task<bool> IsUsedAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        db.DocumentLines.AnyAsync(l => l.CatalogueItemId == itemId, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return db.CatalogueItems.AnyAsync(c => c.Name == trimmed && (exceptId == null || c.Id != exceptId), cancellationToken);
    }

    public async Task<IReadOnlyList<TaxRateOption>> ListTaxRatesAsync(CancellationToken cancellationToken = default)
    {
        var rates = await db.TaxRates.AsNoTracking().Where(t => !t.IsArchived).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return rates.Select(t => new TaxRateOption(t.Id, t.Name, t.Percent.Value)).ToList();
    }
}
