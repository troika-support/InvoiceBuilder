using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Messaging;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Messaging;

public sealed class ReminderPlannerTests
{
    private static readonly ReminderRules Rules = new(3, true, 7, 3);
    private static readonly DateOnly Due = new(2026, 12, 10);

    [Theory]
    [InlineData(2026, 12, 7, ReminderKind.BeforeDue)]
    [InlineData(2026, 12, 10, ReminderKind.OnDueDate)]
    [InlineData(2026, 12, 17, ReminderKind.AfterDue)]
    [InlineData(2026, 12, 31, ReminderKind.AfterDue)]
    public void R30_reminders_fire_on_the_configured_days(int y, int m, int d, ReminderKind expected)
    {
        var decision = ReminderPlanner.Plan(new DateOnly(y, m, d), Rules, [new ReminderItem(0, 1, Due, 100m)]);

        decision.ShouldNotBeNull();
        decision.Kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2026, 12, 8)]
    [InlineData(2026, 12, 18)]
    [InlineData(2027, 1, 7)]
    public void R30_other_days_and_reminders_past_the_maximum_send_nothing(int y, int m, int d)
    {
        ReminderPlanner.Plan(new DateOnly(y, m, d), Rules, [new ReminderItem(0, 1, Due, 100m)]).ShouldBeNull();
    }

    [Fact]
    public void R30_on_due_date_can_be_turned_off()
    {
        ReminderPlanner.Plan(Due, Rules with { OnDueDate = false }, [new ReminderItem(0, 1, Due, 100m)]).ShouldBeNull();
    }

    [Fact]
    public void R30_paid_items_are_skipped()
    {
        ReminderPlanner.Plan(Due, Rules, [new ReminderItem(0, 1, Due, 0m)]).ShouldBeNull();
    }

    [Fact]
    public void R39_two_instalments_hitting_offsets_on_the_same_day_make_one_combined_decision()
    {
        var today = new DateOnly(2026, 12, 17);
        var decision = ReminderPlanner.Plan(
            today,
            Rules,
            [new ReminderItem(1, 3, Due, 800m), new ReminderItem(2, 3, new DateOnly(2026, 12, 20), 800m), new ReminderItem(3, 3, Due.AddDays(14), 800m)]);

        decision.ShouldNotBeNull();
        decision.Items.Select(i => i.Position).ShouldBe([1, 2]);
        decision.Kind.ShouldBe(ReminderKind.AfterDue);
    }
}
