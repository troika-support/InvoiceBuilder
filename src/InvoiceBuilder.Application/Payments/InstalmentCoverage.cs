namespace InvoiceBuilder.Application.Payments;

/// <summary>Describes which instalments a payment covers (R37). Instalment progress is derived from the cumulative amount paid.</summary>
public static class InstalmentCoverage
{
    public static string? Describe(IReadOnlyList<decimal> amounts, decimal paidBefore, decimal applied, Func<decimal, string> money)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(money);
        if (amounts.Count == 0 || applied <= 0m)
        {
            return null;
        }

        var parts = new List<string>();
        var end = paidBefore + applied;
        var start = 0m;
        for (var i = 0; i < amounts.Count; i++)
        {
            var stop = start + amounts[i];
            var overlap = Math.Min(stop, end) - Math.Max(start, paidBefore);
            if (overlap > 0m)
            {
                var number = i + 1;
                if (end >= stop)
                {
                    parts.Add(paidBefore > start ? $"the rest of instalment {number}" : $"instalment {number}");
                }
                else
                {
                    parts.Add($"{money(overlap)} of instalment {number}");
                }
            }

            start = stop;
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var text = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
        return "Covers " + text;
    }
}
