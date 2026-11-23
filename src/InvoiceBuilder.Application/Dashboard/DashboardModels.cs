using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Application.Dashboard;

/// <summary>An issued invoice that may still have money owing. Paid/credited are confirmed totals.</summary>
public sealed record DashboardInvoice(
    Guid Id,
    string Number,
    Guid ClientId,
    string ClientName,
    string? ClientEmail,
    string? ClientPhone,
    string Currency,
    decimal Total,
    decimal Paid,
    decimal Credited,
    DateOnly? DueDate,
    IReadOnlyList<InstalmentDraft> Plan);

/// <summary>An issued quote that is waiting for the client, or accepted and not yet converted.</summary>
public sealed record DashboardQuote(
    Guid Id,
    string Number,
    string ClientName,
    string Currency,
    decimal Total,
    DocumentStatus Status,
    DateOnly? ValidUntil,
    string? AcceptedBy);

public sealed record DashboardPayment(DateTimeOffset PaidAt, string Currency, decimal Amount);

public sealed record DashboardRecurringRun(Guid ScheduleId, DateOnly NextRunDate, string ClientName, bool AutoIssue);

/// <summary>Everything the dashboard is worked out from, for the current business.</summary>
public sealed record DashboardSource(
    IReadOnlyList<DashboardInvoice> OpenInvoices,
    IReadOnlyList<DashboardQuote> Quotes,
    IReadOnlyList<DashboardPayment> Payments,
    IReadOnlyList<DashboardRecurringRun> RecurringRuns);

/// <summary>Read model for the dashboard (spec §4.1). Implemented in Infrastructure.</summary>
public interface IDashboardQueries
{
    /// <param name="paymentsSince">Confirmed payments on or after this instant are returned (start of the 6-month chart).</param>
    /// <param name="recurringUntil">Recurring runs due on or before this date are returned.</param>
    Task<DashboardSource> LoadAsync(DateTimeOffset paymentsSince, DateOnly recurringUntil, CancellationToken cancellationToken = default);
}

/// <summary>Short-lived cache of dashboard figures per business (prompt 22: 60 s, cleared on issue, payment and acceptance).</summary>
public interface IDashboardCache
{
    Task<DashboardFigures> GetOrCreateAsync(Guid businessId, string currency, Func<CancellationToken, Task<DashboardFigures>> create, CancellationToken cancellationToken = default);

    void Invalidate(Guid businessId);
}

// ---- results -------------------------------------------------------------

public sealed record MoneyCount(decimal Amount, int Count);

public sealed record OverdueFigure(decimal Amount, int Count, int? OldestDays);

/// <summary>Collected this month and last; <see cref="ChangePercent"/> is null when last month was zero (shown as "—").</summary>
public sealed record CollectedFigure(decimal ThisMonth, decimal PreviousMonth, string PreviousMonthName, int? ChangePercent);

public sealed record MonthBar(string Label, decimal Amount, bool IsCurrent);

public enum AttentionKind
{
    OverdueInvoice = 0,
    OverdueInstalment = 1,
    InstalmentsDueThisWeek = 2,
    AcceptedQuote = 3,
    UpcomingRecurring = 4,
}

/// <summary>One "Needs attention" row worked out from the data (design-system §8.1 region 4).</summary>
public sealed record AttentionItem(
    AttentionKind Kind,
    string Text,
    Guid? DocumentId = null,
    string? DocumentNumber = null,
    string? Currency = null,
    decimal? Amount = null,
    Guid? ClientId = null,
    string? ClientName = null,
    string? ClientEmail = null,
    string? ClientPhone = null,
    int SortDays = 0);

public sealed record DashboardFigures(
    string Currency,
    MoneyCount Outstanding,
    OverdueFigure Overdue,
    MoneyCount DueThisWeek,
    CollectedFigure Collected,
    MoneyCount QuotesAwaiting,
    IReadOnlyList<MonthBar> CollectedByMonth,
    IReadOnlyList<AttentionItem> Attention);
