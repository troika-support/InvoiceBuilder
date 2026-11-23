using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Application.Clients;

/// <summary>One issued invoice reduced to the facts the client metrics need.</summary>
public sealed record InvoiceFacts(
    string Currency,
    decimal Total,
    DocumentStatus Status,
    decimal Paid,
    decimal Credited,
    DateOnly IssueDate,
    DateOnly? DueDate,
    DateOnly? PaidOn);

/// <summary>Per-client figures. Amounts are always grouped by currency and never summed across currencies (R12).</summary>
public static class ClientMetrics
{
    public static IReadOnlyDictionary<string, decimal> BalancesByCurrency(IEnumerable<InvoiceFacts> invoices) =>
        invoices
            .Where(IsOpen)
            .GroupBy(i => i.Currency)
            .Select(g => (Currency: g.Key, Amount: g.Sum(Outstanding)))
            .Where(x => x.Amount > 0)
            .ToDictionary(x => x.Currency, x => x.Amount);

    public static IReadOnlyDictionary<string, decimal> OverdueByCurrency(IEnumerable<InvoiceFacts> invoices, DateOnly today) =>
        BalancesByCurrency(invoices.Where(i => i.DueDate is { } due && due < today));

    /// <summary>Mean days from issue to payment over fully paid invoices only; null when there are none.</summary>
    public static double? AverageDaysToPay(IEnumerable<InvoiceFacts> invoices)
    {
        var days = invoices
            .Where(i => i.Status == DocumentStatus.Paid && i.PaidOn is not null)
            .Select(i => i.PaidOn!.Value.DayNumber - i.IssueDate.DayNumber)
            .ToList();

        return days.Count == 0 ? null : days.Average();
    }

    public static IReadOnlyDictionary<string, decimal> BilledInYear(IEnumerable<InvoiceFacts> invoices, int year) =>
        invoices
            .Where(i => i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Void && i.IssueDate.Year == year)
            .GroupBy(i => i.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Total));

    private static bool IsOpen(InvoiceFacts invoice) =>
        invoice.Status is DocumentStatus.Issued or DocumentStatus.Sent or DocumentStatus.Viewed or DocumentStatus.PartPaid;

    private static decimal Outstanding(InvoiceFacts invoice) => invoice.Total - invoice.Paid - invoice.Credited;
}
