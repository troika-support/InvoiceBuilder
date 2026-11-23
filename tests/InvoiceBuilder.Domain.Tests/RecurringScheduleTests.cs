using InvoiceBuilder.Domain.Recurring;
using Shouldly;

namespace InvoiceBuilder.Domain.Tests;

public sealed class RecurringScheduleTests
{
    private static RecurringSchedule Make(RecurringCadence cadence, DateOnly start, DateOnly? end = null, int? max = null) =>
        RecurringSchedule.Create(TestData.TenantId, TestData.BusinessId, Guid.NewGuid(), cadence, start, end, max, false, TestData.Now).Value;

    [Fact]
    public void R29_monthly_runs_from_start_date_without_drifting_after_month_end()
    {
        var schedule = Make(RecurringCadence.Monthly, new DateOnly(2026, 1, 31));
        schedule.RecordRun(TestData.Now);
        schedule.NextRunDate.ShouldBe(new DateOnly(2026, 2, 28));
        schedule.RecordRun(TestData.Now);
        schedule.NextRunDate.ShouldBe(new DateOnly(2026, 3, 31));
    }

    [Theory]
    [InlineData(RecurringCadence.Weekly, 2026, 1, 8)]
    [InlineData(RecurringCadence.Quarterly, 2026, 4, 1)]
    [InlineData(RecurringCadence.Yearly, 2027, 1, 1)]
    public void R29_cadence_advances_next_run(RecurringCadence cadence, int y, int m, int d)
    {
        var schedule = Make(cadence, new DateOnly(2026, 1, 1));
        schedule.RecordRun(TestData.Now);
        schedule.NextRunDate.ShouldBe(new DateOnly(y, m, d));
    }

    [Fact]
    public void R29_schedule_finishes_after_max_occurrences()
    {
        var schedule = Make(RecurringCadence.Monthly, new DateOnly(2026, 1, 1), max: 2);
        schedule.IsFinished.ShouldBeFalse();
        schedule.RecordRun(TestData.Now);
        schedule.RecordRun(TestData.Now);
        schedule.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void R29_schedule_finishes_when_next_run_is_after_end_date()
    {
        var schedule = Make(RecurringCadence.Monthly, new DateOnly(2026, 1, 1), end: new DateOnly(2026, 2, 15));
        schedule.RecordRun(TestData.Now);
        schedule.IsFinished.ShouldBeFalse();
        schedule.RecordRun(TestData.Now);
        schedule.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void R29_end_before_start_is_rejected()
    {
        RecurringSchedule.Create(TestData.TenantId, TestData.BusinessId, Guid.NewGuid(), RecurringCadence.Weekly, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null, false, TestData.Now)
            .Error.Code.ShouldBe("Recurring.EndBeforeStart");
    }

    [Fact]
    public void R29_update_recomputes_next_run_from_cadence()
    {
        var schedule = Make(RecurringCadence.Monthly, new DateOnly(2026, 1, 1));
        schedule.RecordRun(TestData.Now);
        schedule.Update(RecurringCadence.Weekly, null, null, true, TestData.Now).IsSuccess.ShouldBeTrue();
        schedule.NextRunDate.ShouldBe(new DateOnly(2026, 1, 8));
        schedule.AutoIssue.ShouldBeTrue();
    }
}
