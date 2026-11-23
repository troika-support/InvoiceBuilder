using InvoiceBuilder.Domain.Messaging;

namespace InvoiceBuilder.Application.Messaging;

public sealed record ReminderRules(int DaysBeforeDue, bool OnDueDate, int RepeatEveryDays, int MaxAfterDue);

/// <summary>One unpaid amount with a due date. Position is 0 for a whole invoice without a plan.</summary>
public sealed record ReminderItem(int Position, int Count, DateOnly Due, decimal Remaining);

public sealed record ReminderDecision(ReminderKind Kind, IReadOnlyList<ReminderItem> Items);

/// <summary>R30 offsets applied per instalment (R39). Several hits on one day become one decision.</summary>
public static class ReminderPlanner
{
    public static ReminderDecision? Plan(DateOnly today, ReminderRules rules, IEnumerable<ReminderItem> unpaid)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(unpaid);
        var hits = new List<(ReminderItem Item, ReminderKind Kind)>();
        foreach (var item in unpaid.Where(i => i.Remaining > 0))
        {
            var kind = KindFor(today, rules, item.Due);
            if (kind is { } k)
            {
                hits.Add((item, k));
            }
        }

        if (hits.Count == 0)
        {
            return null;
        }

        var most = hits.Max(h => h.Kind);
        return new ReminderDecision(most, [.. hits.Select(h => h.Item).OrderBy(i => i.Position)]);
    }

    private static ReminderKind? KindFor(DateOnly today, ReminderRules rules, DateOnly due)
    {
        var daysAfter = today.DayNumber - due.DayNumber;
        if (daysAfter == 0)
        {
            return rules.OnDueDate ? ReminderKind.OnDueDate : null;
        }

        if (daysAfter < 0)
        {
            return rules.DaysBeforeDue > 0 && -daysAfter == rules.DaysBeforeDue ? ReminderKind.BeforeDue : null;
        }

        if (rules.RepeatEveryDays < 1 || daysAfter % rules.RepeatEveryDays != 0)
        {
            return null;
        }

        return daysAfter / rules.RepeatEveryDays <= rules.MaxAfterDue ? ReminderKind.AfterDue : null;
    }
}
