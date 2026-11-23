using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Tests;

public sealed class InstalmentPlannerTests
{
    private static readonly DateOnly First = new(2026, 11, 1);

    [Fact]
    public void R34_equal_split_of_2400_in_three_gives_800_each()
    {
        var plan = InstalmentPlanner.Generate(2400.00m, 3, InstalmentFrequency.Monthly, First).Value;

        plan.Select(p => p.Amount).ShouldBe([800.00m, 800.00m, 800.00m]);
        plan.Select(p => p.DueDate).ShouldBe([new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1)]);
    }

    [Fact]
    public void R34_remainder_goes_on_the_last_instalment()
    {
        var plan = InstalmentPlanner.Generate(100.00m, 3, InstalmentFrequency.Monthly, First).Value;

        plan.Select(p => p.Amount).ShouldBe([33.33m, 33.33m, 33.34m]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public void R34_instalment_count_must_be_between_2_and_24(int count) =>
        InstalmentPlanner.Generate(2400m, count, InstalmentFrequency.Monthly, First).IsFailure.ShouldBeTrue();

    [Theory]
    [InlineData(2)]
    [InlineData(24)]
    public void R34_instalment_count_limits_are_inclusive(int count) =>
        InstalmentPlanner.Generate(2400m, count, InstalmentFrequency.Weekly, First).IsSuccess.ShouldBeTrue();

    [Fact]
    public void R34_frequencies_step_by_the_expected_interval()
    {
        InstalmentPlanner.Generate(300m, 2, InstalmentFrequency.Weekly, First).Value[1].DueDate.ShouldBe(new DateOnly(2026, 11, 8));
        InstalmentPlanner.Generate(300m, 2, InstalmentFrequency.EveryTwoWeeks, First).Value[1].DueDate.ShouldBe(new DateOnly(2026, 11, 15));
        InstalmentPlanner.Generate(300m, 2, InstalmentFrequency.Quarterly, First).Value[1].DueDate.ShouldBe(new DateOnly(2027, 2, 1));
    }

    [Fact]
    public void R34_sum_not_equal_to_total_fails()
    {
        var plan = InstalmentPlanner.Generate(2400m, 3, InstalmentFrequency.Monthly, First).Value;

        InstalmentPlanner.Validate(plan, 2500m, null).Error.Code.ShouldBe("Instalment.SumMismatch");
    }

    [Fact]
    public void R34_dates_must_be_strictly_increasing()
    {
        InstalmentDraft[] plan =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 1200m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 1200m),
        ];

        InstalmentPlanner.Validate(plan, 2400m, null).Error.Code.ShouldBe("Instalment.DatesNotIncreasing");
    }

    [Fact]
    public void R34_first_date_must_not_be_before_the_issue_date()
    {
        var plan = InstalmentPlanner.Generate(2400m, 3, InstalmentFrequency.Monthly, First).Value;

        InstalmentPlanner.Validate(plan, 2400m, new DateOnly(2026, 11, 2)).Error.Code.ShouldBe("Instalment.FirstDateBeforeIssue");
        InstalmentPlanner.Validate(plan, 2400m, First).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void R34_amounts_must_be_positive()
    {
        InstalmentDraft[] plan =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 0m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 8), 2400m),
        ];

        InstalmentPlanner.Validate(plan, 2400m, null).Error.Code.ShouldBe("Instalment.AmountNotPositive");
    }

    [Fact]
    public void R35_first_instalment_can_be_due_on_acceptance_but_later_ones_cannot()
    {
        InstalmentDraft[] ok =
        [
            new(InstalmentDueKind.OnAcceptance, null, 800m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 1600m),
        ];
        InstalmentDraft[] bad =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m),
            new(InstalmentDueKind.OnIssue, null, 1600m),
        ];

        InstalmentPlanner.Validate(ok, 2400m, null).IsSuccess.ShouldBeTrue();
        InstalmentPlanner.Validate(bad, 2400m, null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void R36_reschedule_of_a_paid_instalment_fails()
    {
        var states = StatesWith(paid: 800m);
        InstalmentDraft[] proposed =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 700m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 15), 900m),
            new(InstalmentDueKind.Date, new DateOnly(2027, 1, 15), 800m),
        ];

        InstalmentPlanner.Reschedule(states, proposed, "Client request").Error.Code.ShouldBe("Instalment.Locked");
    }

    [Fact]
    public void R36_reschedule_of_a_part_paid_instalment_fails()
    {
        var states = StatesWith(paid: 1000m);
        InstalmentDraft[] proposed =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 15), 800m),
            new(InstalmentDueKind.Date, new DateOnly(2027, 1, 15), 800m),
        ];

        InstalmentPlanner.Reschedule(states, proposed, "Client request").Error.Code.ShouldBe("Instalment.Locked");
    }

    [Fact]
    public void R36_unpaid_amounts_must_sum_to_the_balance_due()
    {
        var states = StatesWith(paid: 800m);
        InstalmentDraft[] proposed =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 15), 900m),
            new(InstalmentDueKind.Date, new DateOnly(2027, 1, 15), 600m),
        ];

        InstalmentPlanner.Reschedule(states, proposed, "Client request").Error.Code.ShouldBe("Instalment.SumMismatch");
    }

    [Fact]
    public void R36_reschedule_of_unpaid_instalments_succeeds_when_locked_ones_are_unchanged()
    {
        var states = StatesWith(paid: 800m);
        InstalmentDraft[] proposed =
        [
            new(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m),
            new(InstalmentDueKind.Date, new DateOnly(2026, 12, 15), 900m),
            new(InstalmentDueKind.Date, new DateOnly(2027, 1, 15), 700m),
        ];

        InstalmentPlanner.Reschedule(states, proposed, "Client request").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void R36_reschedule_requires_a_reason()
    {
        var states = StatesWith(paid: 0m);

        InstalmentPlanner.Reschedule(states, [.. states.Select(s => new InstalmentDraft(s.DueKind, s.DueDate, s.Amount))], " ")
            .Error.Code.ShouldBe("Instalment.ReasonRequired");
    }

    private static IReadOnlyList<InstalmentState> StatesWith(decimal paid) =>
        InstalmentStatusCalculator.Calculate(ThreeBy800(), paid, 0m, new DateOnly(2026, 10, 23));

    internal static IReadOnlyList<InstalmentDraft> ThreeBy800() =>
    [
        new(InstalmentDueKind.Date, new DateOnly(2026, 11, 1), 800m),
        new(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m),
        new(InstalmentDueKind.Date, new DateOnly(2027, 1, 1), 800m),
    ];
}
