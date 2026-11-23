using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>Generates and validates instalment plans (R34, R35) and reschedules them (R36). Pure.</summary>
public static class InstalmentPlanner
{
    public const int MinCount = 2;
    public const int MaxCount = 24;

    private static Error Fail(string code, string message) => new($"Instalment.{code}", message);

    /// <summary>
    /// Equal amounts rounded down to the cent, with the remainder on the last instalment.
    /// <paramref name="firstDueDate"/> is the date of instalment 1 when its kind is Date; later dates step from it.
    /// </summary>
    public static Result<IReadOnlyList<InstalmentDraft>> Generate(
        decimal total,
        int count,
        InstalmentFrequency frequency,
        DateOnly firstDueDate,
        InstalmentDueKind firstDueKind = InstalmentDueKind.Date)
    {
        if (count is < MinCount or > MaxCount)
        {
            return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("Count", $"A plan needs between {MinCount} and {MaxCount} instalments."));
        }

        if (total <= 0m)
        {
            return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("TotalNotPositive", "The document total must be above zero to split it."));
        }

        var share = Math.Floor(total / count * 100m) / 100m;
        var drafts = new List<InstalmentDraft>(count);
        for (var i = 0; i < count; i++)
        {
            var amount = i == count - 1 ? total - (share * (count - 1)) : share;
            DateOnly? date = i == 0 && firstDueKind != InstalmentDueKind.Date ? null : DateFor(firstDueDate, frequency, i);
            drafts.Add(new InstalmentDraft(i == 0 ? firstDueKind : InstalmentDueKind.Date, date, amount));
        }

        return Result.Success<IReadOnlyList<InstalmentDraft>>(drafts);
    }

    public static Result Validate(IReadOnlyList<InstalmentDraft> drafts, decimal total, DateOnly? issueDate)
    {
        if (drafts.Count is < MinCount or > MaxCount)
        {
            return Result.Failure(Fail("Count", $"A plan needs between {MinCount} and {MaxCount} instalments."));
        }

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            if (draft.Amount <= 0m)
            {
                return Result.Failure(Fail("AmountNotPositive", "Every instalment amount must be above zero."));
            }

            if (draft.DueKind != InstalmentDueKind.Date && i > 0)
            {
                return Result.Failure(Fail("DueKindNotAllowed", "Only the first instalment can be due on acceptance or on issue."));
            }

            if (draft.DueKind == InstalmentDueKind.Date && draft.DueDate is null)
            {
                return Result.Failure(Fail("DateRequired", "Every dated instalment needs a due date."));
            }
        }

        if (drafts.Sum(d => d.Amount) != total)
        {
            return Result.Failure(Fail("SumMismatch", "The instalments must add up to the document total exactly."));
        }

        var dated = drafts.Where(d => d.DueDate.HasValue).Select(d => d.DueDate!.Value).ToList();
        for (var i = 1; i < dated.Count; i++)
        {
            if (dated[i] <= dated[i - 1])
            {
                return Result.Failure(Fail("DatesNotIncreasing", "Instalment dates must be strictly increasing."));
            }
        }

        if (issueDate is { } issue && drafts[0].DueKind == InstalmentDueKind.Date && drafts[0].DueDate < issue)
        {
            return Result.Failure(Fail("FirstDateBeforeIssue", "The first instalment cannot be due before the issue date."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Reschedule rules (R36): a reason is required, paid and part-paid instalments are locked, and the new
    /// unpaid amounts must add up to the balance still due on the unpaid instalments.
    /// TODO(question): after a credit note, stored amounts and derived amounts differ; confirm how Reschedule should treat them.
    /// </summary>
    public static Result<IReadOnlyList<InstalmentDraft>> Reschedule(
        IReadOnlyList<InstalmentState> current,
        IReadOnlyList<InstalmentDraft> proposed,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("ReasonRequired", "A reason is required to reschedule."));
        }

        if (proposed.Count != current.Count)
        {
            return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("CountChanged", "Rescheduling cannot add or remove instalments."));
        }

        var unpaidSum = 0m;
        var expectedUnpaidSum = 0m;
        for (var i = 0; i < current.Count; i++)
        {
            var state = current[i];
            var next = proposed[i];
            if (state.Paid > 0m)
            {
                if (next.Amount != state.Amount || next.DueDate != state.DueDate)
                {
                    return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("Locked", $"Instalment {state.Position} has payments and cannot be changed."));
                }
            }
            else
            {
                unpaidSum += next.Amount;
                expectedUnpaidSum += state.Remaining;
            }
        }

        if (unpaidSum != expectedUnpaidSum)
        {
            return Result.Failure<IReadOnlyList<InstalmentDraft>>(Fail("SumMismatch", "The unpaid instalments must add up to the balance due."));
        }

        var total = proposed.Sum(p => p.Amount);
        var validation = Validate(proposed, total, null);
        return validation.IsFailure
            ? Result.Failure<IReadOnlyList<InstalmentDraft>>(validation.Error)
            : Result.Success(proposed);
    }

    private static DateOnly DateFor(DateOnly first, InstalmentFrequency frequency, int index) => frequency switch
    {
        InstalmentFrequency.Weekly => first.AddDays(7 * index),
        InstalmentFrequency.EveryTwoWeeks => first.AddDays(14 * index),
        InstalmentFrequency.Monthly => first.AddMonths(index),
        InstalmentFrequency.Quarterly => first.AddMonths(3 * index),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency)),
    };
}
