using System.Globalization;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Application.Dashboard;

/// <summary>
/// Works out the dashboard figures (spec §4.1) and the derived "Needs attention" rows (design-system §8.1).
/// Pure: no I/O, so every rule is unit-tested. Figures are for one currency only and never mix USD and ZiG (R12).
/// </summary>
public static class DashboardCalculator
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public const int ChartMonths = 6;

    public const int DueSoonDays = 7;

    /// <summary>Start of the oldest chart month, as an instant, so the query loads only the payments it needs.</summary>
    public static DateTimeOffset ChartStart(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var first = new DateTime(local.Year, local.Month, 1).AddMonths(-(ChartMonths - 1));
        return new DateTimeOffset(first, zone.GetUtcOffset(first));
    }

    public static DashboardFigures Calculate(DashboardSource source, string currency, DateTimeOffset now, TimeZoneInfo zone, IDisplayFormatter format)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(format);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(local.DateTime);

        var invoices = source.OpenInvoices.Select(i => Analyse(i, today)).Where(a => a.Balance > 0m).ToList();
        var mine = invoices.Where(a => Same(a.Invoice.Currency, currency)).ToList();

        var outstanding = new MoneyCount(mine.Sum(a => a.Balance), mine.Count);
        var overdueList = mine.Where(a => a.OverdueAmount > 0m).ToList();
        var overdue = new OverdueFigure(
            overdueList.Sum(a => a.OverdueAmount),
            overdueList.Count,
            overdueList.Count == 0 ? null : overdueList.Max(a => a.DaysLate));
        var dueSoon = mine.SelectMany(a => a.DueSoon).ToList();
        var dueThisWeek = new MoneyCount(dueSoon.Sum(s => s.Remaining), dueSoon.Count);

        var months = Enumerable.Range(0, ChartMonths)
            .Select(back => new DateTime(local.Year, local.Month, 1).AddMonths(-(ChartMonths - 1 - back)))
            .ToList();
        var byMonth = source.Payments
            .Where(p => Same(p.Currency, currency))
            .GroupBy(p => MonthKey(TimeZoneInfo.ConvertTime(p.PaidAt, zone)))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var bars = months
            .Select((m, i) => new MonthBar(m.ToString("MMM", Culture), byMonth.GetValueOrDefault(MonthKey(m)), i == ChartMonths - 1))
            .ToList();
        var thisMonth = bars[^1].Amount;
        var previous = bars[^2].Amount;
        var collected = new CollectedFigure(thisMonth, previous, months[^2].ToString("MMMM", Culture), ChangePercent(thisMonth, previous));

        var awaiting = source.Quotes
            .Where(q => Same(q.Currency, currency) && IsAwaitingReply(q, today))
            .ToList();
        var quotesAwaiting = new MoneyCount(awaiting.Sum(q => q.Total), awaiting.Count);

        return new DashboardFigures(
            currency,
            outstanding,
            overdue,
            dueThisWeek,
            collected,
            quotesAwaiting,
            bars,
            Attention(source, invoices, format));
    }

    /// <summary>Whole-number % change; null when there is nothing to compare with.</summary>
    public static int? ChangePercent(decimal current, decimal previous) =>
        previous == 0m ? null : (int)Math.Round((current - previous) / previous * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Issued, sent or viewed, and still valid today.</summary>
    public static bool IsAwaitingReply(DashboardQuote quote, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return quote.Status is DocumentStatus.Issued or DocumentStatus.Sent or DocumentStatus.Viewed
            && (quote.ValidUntil is not { } until || until >= today);
    }

    private static List<AttentionItem> Attention(DashboardSource source, List<Analysed> invoices, IDisplayFormatter format)
    {
        var items = new List<AttentionItem>();

        foreach (var a in invoices.Where(a => a.OverdueAmount > 0m).OrderByDescending(a => a.DaysLate))
        {
            var inv = a.Invoice;
            var days = a.DaysLate ?? 0;
            if (a.OldestOverdue is { } instalment)
            {
                items.Add(new AttentionItem(
                    AttentionKind.OverdueInstalment,
                    $"{inv.Number} · instalment {instalment.Position} of {inv.Plan.Count} is {format.DaysLate(days)} · {format.Money(a.OverdueAmount, inv.Currency)} overdue",
                    inv.Id, inv.Number, inv.Currency, a.OverdueAmount, inv.ClientId, inv.ClientName, inv.ClientEmail, inv.ClientPhone, days));
            }
            else
            {
                items.Add(new AttentionItem(
                    AttentionKind.OverdueInvoice,
                    $"{inv.Number} · {inv.ClientName} · {format.DaysLate(days)} · {format.Money(a.OverdueAmount, inv.Currency)} due",
                    inv.Id, inv.Number, inv.Currency, a.OverdueAmount, inv.ClientId, inv.ClientName, inv.ClientEmail, inv.ClientPhone, days));
            }
        }

        foreach (var group in invoices.SelectMany(a => a.DueSoon.Select(s => (a.Invoice.Currency, s.Remaining))).GroupBy(x => x.Currency).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var count = group.Count();
            var amount = group.Sum(x => x.Remaining);
            items.Add(new AttentionItem(
                AttentionKind.InstalmentsDueThisWeek,
                $"{count} {(count == 1 ? "instalment" : "instalments")} due this week · {format.Money(amount, group.Key)}",
                Currency: group.Key,
                Amount: amount));
        }

        foreach (var q in source.Quotes.Where(q => q.Status == DocumentStatus.Accepted))
        {
            var by = string.IsNullOrWhiteSpace(q.AcceptedBy) ? string.Empty : $" by {q.AcceptedBy.Trim()}";
            items.Add(new AttentionItem(
                AttentionKind.AcceptedQuote,
                $"{q.Number} for {format.Money(q.Total, q.Currency)} was accepted{by}",
                q.Id, q.Number, q.Currency, q.Total, ClientName: q.ClientName));
        }

        foreach (var r in source.RecurringRuns.OrderBy(r => r.NextRunDate))
        {
            var mode = r.AutoIssue ? "issues and sends automatically" : "creates a draft";
            items.Add(new AttentionItem(
                AttentionKind.UpcomingRecurring,
                $"Recurring invoice for {r.ClientName} runs {r.NextRunDate.ToString("d MMM", Culture)} and {mode}",
                r.ScheduleId,
                ClientName: r.ClientName));
        }

        return items;
    }

    private sealed record Analysed(
        DashboardInvoice Invoice,
        decimal Balance,
        decimal OverdueAmount,
        int? DaysLate,
        InstalmentState? OldestOverdue,
        IReadOnlyList<InstalmentState> DueSoon);

    private static Analysed Analyse(DashboardInvoice invoice, DateOnly today)
    {
        var balance = invoice.Total - invoice.Paid - invoice.Credited;
        if (invoice.Plan.Count == 0)
        {
            var late = invoice.DueDate is { } due && due < today && balance > 0m;
            return new Analysed(invoice, balance, late ? balance : 0m, late ? today.DayNumber - invoice.DueDate!.Value.DayNumber : null, null, []);
        }

        // R38: an invoice with a plan is overdue only by its overdue instalments.
        var states = InstalmentStatusCalculator.Calculate(invoice.Plan, invoice.Paid, invoice.Credited, today);
        var overdue = states.Where(s => s.Status == InstalmentStatus.Overdue).OrderBy(s => s.DueDate).ToList();
        var oldest = overdue.FirstOrDefault();
        var soon = states
            .Where(s => s.Remaining > 0m && s.Status != InstalmentStatus.Overdue
                && (s.DueDate ?? today) is var d && d >= today && d.DayNumber - today.DayNumber <= DueSoonDays)
            .ToList();
        return new Analysed(
            invoice,
            balance,
            InstalmentStatusCalculator.OverdueAmount(states),
            oldest?.DueDate is { } oldestDue ? today.DayNumber - oldestDue.DayNumber : null,
            oldest,
            soon);
    }

    private static int MonthKey(DateTimeOffset value) => (value.Year * 12) + value.Month;

    private static int MonthKey(DateTime value) => (value.Year * 12) + value.Month;

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
