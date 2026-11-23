namespace InvoiceBuilder.Application.Catalogue;

public sealed record CatalogueItemRow(
    Guid Id,
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string Currency,
    Guid? TaxRateId,
    string? TaxName,
    string? HsCode,
    bool IsRecurring,
    bool IsArchived,
    bool IsUsed);

/// <summary>What "Add from catalogue…" needs to build a line (R13, R13a: the HS code is copied).</summary>
public sealed record CatalogueSearchHit(
    Guid Id,
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string Currency,
    Guid? TaxRateId,
    string? HsCode);

public sealed record TaxRateOption(Guid Id, string Name, decimal Percent);

public interface ICatalogueQueries
{
    Task<IReadOnlyList<CatalogueItemRow>> ListAsync(string? search, bool includeArchived, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogueSearchHit>> SearchAsync(string term, int take, CancellationToken cancellationToken = default);

    Task<bool> IsUsedAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxRateOption>> ListTaxRatesAsync(CancellationToken cancellationToken = default);
}
