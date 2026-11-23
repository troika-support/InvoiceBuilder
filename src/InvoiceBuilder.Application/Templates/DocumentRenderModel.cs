namespace InvoiceBuilder.Application.Templates;

/// <summary>Everything a template can bind to (template-rendering.md §3). Raw values; the renderer formats them.</summary>
public sealed record DocumentRenderModel(
    BusinessRenderModel Business,
    ClientRenderModel Client,
    DocumentRenderInfo Document,
    IReadOnlyList<RenderLine> Lines,
    IReadOnlyDictionary<string, string> ThemeTokens)
{
    public PlanRenderInfo? Plan { get; init; }

    public AcceptanceRenderInfo? Acceptance { get; init; }
}

public sealed record BusinessRenderModel(
    string TradingName,
    string? RegisteredName,
    string? Email,
    string? Phone,
    string? Address,
    string? LogoDataUri)
{
    public string? BankDetails { get; init; }
}

public sealed record ClientRenderModel(string Name, string? Email, string? Phone, string? Address);

public sealed record DocumentRenderInfo(
    string TypeLabel,
    string Number,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateOnly? ValidUntil,
    string Currency,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    decimal AmountPaid,
    decimal Balance,
    string? Notes,
    string? Terms,
    string? ClientLinkUrl)
{
    public string? Status { get; init; }
}

public sealed record RenderLine(string Description, string? Detail, decimal Quantity, decimal Rate, string TaxLabel, decimal Amount);

public sealed record PlanRenderInfo(int Count, int? NextNumber, decimal? NextAmount, DateOnly? NextDueDate, decimal OverdueAmount)
{
    public IReadOnlyList<InstalmentRenderRow> Rows { get; init; } = [];
}

public sealed record InstalmentRenderRow(int Number, DateOnly? DueDate, decimal Amount, string Status, decimal Remaining);

public sealed record AcceptanceRenderInfo(string SignerName, DateTimeOffset AcceptedAt);
