using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>
/// Input for a line. Tax rate, fiscal tax ID and HS code are copied onto the line as a snapshot (R13, R13a).
/// <paramref name="CatalogueItemId"/> only records where the line came from; the line never reads the item again.
/// </summary>
public sealed record LineInput(
    string Description,
    string? Detail,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineDiscount = 0m,
    TaxRate? TaxRate = null,
    HsCode? HsCode = null,
    Guid? CatalogueItemId = null);

public sealed class DocumentLine : BusinessEntity
{
    private DocumentLine()
    {
    }

    public Guid DocumentId { get; private set; }

    public int Position { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Detail { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public Guid? TaxRateId { get; private set; }

    public decimal TaxPercentSnapshot { get; private set; }

    public string? FiscalTaxIdSnapshot { get; private set; }

    public HsCode? HsCodeSnapshot { get; private set; }

    public decimal LineDiscount { get; private set; }

    public Guid? CatalogueItemId { get; private set; }

    /// <summary>Quantity × unit price less the line discount, before the document discount and tax.</summary>
    public decimal LineTotal { get; private set; }

    internal static DocumentLine Create(Document document, int position, LineInput input, DateTimeOffset now)
    {
        var line = new DocumentLine
        {
            TenantId = document.TenantId,
            BusinessId = document.BusinessId,
            DocumentId = document.Id,
            Position = position,
            Description = input.Description,
            Detail = input.Detail,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            TaxRateId = input.TaxRate?.Id,
            TaxPercentSnapshot = input.TaxRate?.Percent.Value ?? 0m,
            FiscalTaxIdSnapshot = input.TaxRate?.FiscalTaxId,
            HsCodeSnapshot = input.HsCode,
            LineDiscount = input.LineDiscount,
            CatalogueItemId = input.CatalogueItemId,
        };
        line.Initialise(now);
        return line;
    }

    internal void SetPosition(int position) => Position = position;

    /// <summary>The line as input, keeping its tax snapshot (used by Duplicate).</summary>
    internal LineInput ToInput() => new(Description, Detail, Quantity, UnitPrice, LineDiscount, null, HsCodeSnapshot, CatalogueItemId);

    internal void CopyTaxSnapshot(DocumentLine source)
    {
        TaxRateId = source.TaxRateId;
        TaxPercentSnapshot = source.TaxPercentSnapshot;
        FiscalTaxIdSnapshot = source.FiscalTaxIdSnapshot;
    }

    internal void SetLineTotal(decimal lineTotal) => LineTotal = lineTotal;
}
