using InvoiceBuilder.Domain.Documents;
using Microsoft.Extensions.Time.Testing;

namespace InvoiceBuilder.Domain.Tests;

public sealed class InstalmentStatusCalculatorTests
{
    private static DateOnly Today(FakeTimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static FakeTimeProvider ClockAt(int year, int month, int day) =>
        new(new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void R37_payments_apply_oldest_due_first()
    {
        var clock = ClockAt(2026, 10, 23);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 1000m, 0m, Today(clock));

        states[0].Status.ShouldBe(InstalmentStatus.Paid);
        states[1].Status.ShouldBe(InstalmentStatus.PartPaid);
        states[1].Remaining.ShouldBe(600m);
        states[1].Paid.ShouldBe(200m);
        states[2].Status.ShouldBe(InstalmentStatus.Upcoming);
        states[2].Remaining.ShouldBe(800m);
    }

    [Fact]
    public void R37_part_paid_instalment_shows_the_amount_left()
    {
        var clock = ClockAt(2026, 10, 23);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 1000m, 0m, Today(clock));

        states[1].Remaining.ShouldBe(600m);
    }

    [Fact]
    public void R37_credit_notes_reduce_instalments_from_the_last_backwards()
    {
        var clock = ClockAt(2026, 10, 23);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 0m, 300m, Today(clock));

        states[0].EffectiveAmount.ShouldBe(800m);
        states[1].EffectiveAmount.ShouldBe(800m);
        states[2].EffectiveAmount.ShouldBe(500m);
    }

    [Fact]
    public void R37_credit_larger_than_the_last_instalment_rolls_to_the_previous_one()
    {
        var clock = ClockAt(2026, 10, 23);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 0m, 1000m, Today(clock));

        states[2].EffectiveAmount.ShouldBe(0m);
        states[1].EffectiveAmount.ShouldBe(600m);
    }

    [Fact]
    public void R37_instalment_due_today_is_due_and_future_is_upcoming()
    {
        var clock = ClockAt(2026, 11, 1);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 0m, 0m, Today(clock));

        states[0].Status.ShouldBe(InstalmentStatus.Due);
        states[1].Status.ShouldBe(InstalmentStatus.Upcoming);
    }

    [Fact]
    public void R37_overdue_is_derived_from_the_clock()
    {
        var clock = ClockAt(2026, 12, 5);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 0m, 0m, Today(clock));

        states[0].Status.ShouldBe(InstalmentStatus.Overdue);
        states[1].Status.ShouldBe(InstalmentStatus.Overdue);
        states[2].Status.ShouldBe(InstalmentStatus.Upcoming);

        var earlier = ClockAt(2026, 10, 25);
        InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 0m, 0m, Today(earlier))[0].Status
            .ShouldBe(InstalmentStatus.Upcoming);
    }

    [Fact]
    public void R38_overdue_amount_is_the_remaining_of_overdue_instalments_only()
    {
        var clock = ClockAt(2026, 12, 5);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 1000m, 0m, Today(clock));

        InstalmentStatusCalculator.OverdueAmount(states).ShouldBe(600m);
        states.Sum(s => s.Remaining).ShouldBe(1400m);
    }

    [Fact]
    public void R37_overpaying_rolls_into_the_next_instalment()
    {
        var clock = ClockAt(2026, 10, 23);

        var states = InstalmentStatusCalculator.Calculate(InstalmentPlannerTests.ThreeBy800(), 2400m, 0m, Today(clock));

        states.ShouldAllBe(s => s.Status == InstalmentStatus.Paid);
    }

    [Fact]
    public void R37_invoice_due_date_is_the_last_instalment_date()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 2400m), TestData.Now);
        invoice.SetPlan(InstalmentPlannerTests.ThreeBy800(), InstalmentFrequency.Monthly, TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.Issue(TestData.NewIssueRequest("INV-0001", new DateOnly(2026, 10, 23)), TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.DueDate.ShouldBe(new DateOnly(2027, 1, 1));
        invoice.Instalments.Count.ShouldBe(3);
    }

    [Fact]
    public void R5_plan_cannot_change_after_issue()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 2400m), TestData.Now);
        invoice.Issue(TestData.NewIssueRequest("INV-0001", new DateOnly(2026, 10, 23)), TestData.Now);

        invoice.SetPlan(InstalmentPlannerTests.ThreeBy800(), InstalmentFrequency.Monthly, TestData.Now).Error.Code.ShouldBe("Document.NotEditable");
    }
}
