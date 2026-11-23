namespace InvoiceBuilder.Domain.Documents;

/// <summary>Derives instalment state from confirmed payments and credit notes (R37, R38). Pure.</summary>
public static class InstalmentStatusCalculator
{
    /// <summary>
    /// Credit-note amounts reduce instalments from the last backwards; confirmed allocations are applied oldest first.
    /// An unpaid remainder on a past due date is Overdue, including a part-paid instalment (R38).
    /// An instalment with no date yet (due on acceptance/issue, not resolved) counts as due today.
    /// </summary>
    public static IReadOnlyList<InstalmentState> Calculate(
        IReadOnlyList<InstalmentDraft> instalments,
        decimal confirmedPaid,
        decimal creditedTotal,
        DateOnly today)
    {
        var effective = instalments.Select(i => i.Amount).ToArray();
        var credit = creditedTotal;
        for (var i = effective.Length - 1; i >= 0 && credit > 0m; i--)
        {
            var reduction = Math.Min(credit, effective[i]);
            effective[i] -= reduction;
            credit -= reduction;
        }

        var states = new List<InstalmentState>(instalments.Count);
        var pool = confirmedPaid;
        for (var i = 0; i < instalments.Count; i++)
        {
            var applied = Math.Min(pool, effective[i]);
            pool -= applied;
            var remaining = effective[i] - applied;
            var due = instalments[i].DueDate ?? today;

            var status = remaining == 0m ? InstalmentStatus.Paid
                : due < today ? InstalmentStatus.Overdue
                : applied > 0m ? InstalmentStatus.PartPaid
                : due == today ? InstalmentStatus.Due
                : InstalmentStatus.Upcoming;

            states.Add(new InstalmentState(i + 1, instalments[i].DueKind, instalments[i].DueDate, instalments[i].Amount, effective[i], applied, remaining, status));
        }

        return states;
    }

    public static decimal OverdueAmount(IEnumerable<InstalmentState> states) =>
        states.Where(s => s.Status == InstalmentStatus.Overdue).Sum(s => s.Remaining);
}
