using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Catalogue;

public sealed class CatalogueItem : BusinessEntity
{
    private CatalogueItem()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Unit { get; private set; }

    public decimal UnitPrice { get; private set; }

    public CurrencyCode Currency { get; private set; } = CurrencyCode.Usd;

    public Guid? TaxRateId { get; private set; }

    public HsCode? HsCode { get; private set; }

    public bool IsRecurring { get; private set; }

    public bool IsArchived { get; private set; }

    public static Result<CatalogueItem> Create(
        Guid tenantId,
        Guid businessId,
        string name,
        decimal unitPrice,
        CurrencyCode currency,
        DateTimeOffset now,
        Guid? taxRateId = null,
        HsCode? hsCode = null,
        bool isRecurring = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<CatalogueItem>(new Error("Catalogue.NameRequired", "An item needs a name."));
        }

        if (unitPrice < 0m)
        {
            return Result.Failure<CatalogueItem>(new Error("Catalogue.PriceNegative", "A unit price cannot be negative."));
        }

        var item = new CatalogueItem
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name.Trim(),
            UnitPrice = unitPrice,
            Currency = currency,
            TaxRateId = taxRateId,
            HsCode = hsCode,
            IsRecurring = isRecurring,
        };
        item.Initialise(now);
        return Result.Success(item);
    }

    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        Touch(now);
    }

    public void Restore(DateTimeOffset now)
    {
        IsArchived = false;
        Touch(now);
    }

    public Result Update(
        string name,
        string? description,
        string? unit,
        decimal unitPrice,
        CurrencyCode currency,
        Guid? taxRateId,
        HsCode? hsCode,
        bool isRecurring,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("Catalogue.NameRequired", "An item needs a name."));
        }

        if (unitPrice < 0m)
        {
            return Result.Failure(new Error("Catalogue.PriceNegative", "A unit price cannot be negative."));
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        UnitPrice = unitPrice;
        Currency = currency;
        TaxRateId = taxRateId;
        HsCode = hsCode;
        IsRecurring = isRecurring;
        Touch(now);
        return Result.Success();
    }
}
