using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Businesses;

public sealed class TaxRate : BusinessEntity
{
    private TaxRate()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public TaxPercent Percent { get; private set; } = TaxPercent.Zero;

    public DateOnly EffectiveFrom { get; private set; }

    public string? FiscalTaxId { get; private set; }

    public bool IsArchived { get; private set; }

    public static TaxRate Create(
        Guid tenantId,
        Guid businessId,
        string name,
        TaxPercent percent,
        DateOnly effectiveFrom,
        string? fiscalTaxId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var rate = new TaxRate
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name.Trim(),
            Percent = percent,
            EffectiveFrom = effectiveFrom,
            FiscalTaxId = string.IsNullOrWhiteSpace(fiscalTaxId) ? null : fiscalTaxId.Trim(),
        };
        rate.Initialise(now);
        return rate;
    }

    public void ChangePercent(TaxPercent percent, DateTimeOffset now)
    {
        Percent = percent;
        Touch(now);
    }

    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        Touch(now);
    }
}
