namespace InvoiceBuilder.Application.Documents;

/// <summary>One instalment reduced to what the overdue rule needs.</summary>
public sealed record OverdueInstalment(DateOnly DueDate, decimal Amount);

/// <summary>R4 / R37 / R38: derives "days late" for an invoice. Never stored.</summary>
public static class OverdueCalculator
{
    /// <summary>
    /// Returns the days late, or null when the invoice is not overdue.
    /// Plain invoices: due date passed and balance &gt; 0. Invoices with a plan: only when an instalment is overdue,
    /// measured from the oldest overdue instalment. Paid amounts apply oldest first; credits reduce from the last backwards.
    /// </summary>
    public static int? DaysLate(DateOnly? dueDate, decimal total, decimal paid, decimal credited, IReadOnlyList<OverdueInstalment> plan, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Count == 0)
        {
            var balance = total - paid - credited;
            return dueDate is { } due && due < today && balance > 0 ? today.DayNumber - due.DayNumber : null;
        }

        var ordered = plan.OrderBy(p => p.DueDate).Select(p => p.Amount).ToArray();
        var dates = plan.OrderBy(p => p.DueDate).Select(p => p.DueDate).ToArray();

        var credit = credited;
        for (var i = ordered.Length - 1; i >= 0 && credit > 0; i--)
        {
            var take = Math.Min(ordered[i], credit);
            ordered[i] -= take;
            credit -= take;
        }

        var toApply = paid;
        for (var i = 0; i < ordered.Length; i++)
        {
            var take = Math.Min(ordered[i], Math.Max(toApply, 0m));
            ordered[i] -= take;
            toApply -= take;

            if (ordered[i] > 0 && dates[i] < today)
            {
                return today.DayNumber - dates[i].DayNumber;
            }
        }

        return null;
    }
}
