using InvoiceBuilder.Application.Dashboard;
using InvoiceBuilder.Application.Formatting;
using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Application.Tests.Dashboard;

public sealed class DashboardCalculatorTests
{
    // 23 Nov 2026, 10:00 in Harare.
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 23);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Harare");
    private static readonly DisplayFormatter Format = new("Africa/Harare");

    private static DashboardInvoice Invoice(string number, string currency, decimal total, DateOnly? due, decimal paid = 0m, decimal credited = 0m, params InstalmentDraft[] plan) =>
        new(Guid.NewGuid(), number, Guid.NewGuid(), "Harare Builders Co.", "a@b.co", "+263770000000", currency, total, paid, credited, due, plan);

    private static DashboardQuote Quote(string number, string currency, decimal total, DocumentStatus status, DateOnly? validUntil, string? acceptedBy = null) =>
        new(Guid.NewGuid(), number, "Harare Builders Co.", currency, total, status, validUntil, acceptedBy);

    private static DashboardPayment Paid(int year, int month, int day, string currency, decimal amount) =>
        new(new DateTimeOffset(year, month, day, 10, 0, 0, TimeSpan.FromHours(2)), currency, amount);

    private static DashboardFigures Calc(DashboardSource source, string currency = "USD") =>
        DashboardCalculator.Calculate(source, currency, Now, Zone, Format);

    private static DashboardSource Source(
        IReadOnlyList<DashboardInvoice>? invoices = null,
        IReadOnlyList<DashboardQuote>? quotes = null,
        IReadOnlyList<DashboardPayment>? payments = null,
        IReadOnlyList<DashboardRecurringRun>? runs = null) =>
        new(invoices ?? [], quotes ?? [], payments ?? [], runs ?? []);

    [Fact]
    public void R12_usd_and_zig_figures_are_never_added_together()
    {
        var source = Source(
            [Invoice("INV-0001", "USD", 100m, Today.AddDays(5)), Invoice("INV-0002", "ZWG", 5000m, Today.AddDays(5))],
            payments: [Paid(2026, 11, 2, "USD", 40m), Paid(2026, 11, 3, "ZWG", 900m)]);

        var usd = Calc(source, "USD");
        var zig = Calc(source, "ZWG");

        usd.Outstanding.ShouldBe(new MoneyCount(100m, 1));
        zig.Outstanding.ShouldBe(new MoneyCount(5000m, 1));
        usd.Collected.ThisMonth.ShouldBe(40m);
        zig.Collected.ThisMonth.ShouldBe(900m);
    }

    [Fact]
    public void Outstanding_uses_balances_after_payments_and_credit_notes_and_skips_settled_invoices()
    {
        var source = Source([
            Invoice("INV-0001", "USD", 1000m, Today.AddDays(10), paid: 300m, credited: 100m),
            Invoice("INV-0002", "USD", 200m, Today.AddDays(10), paid: 150m, credited: 50m),
        ]);

        Calc(source).Outstanding.ShouldBe(new MoneyCount(600m, 1));
    }

    [Fact]
    public void Overdue_counts_plain_invoices_past_due_with_the_oldest_days()
    {
        var source = Source([
            Invoice("INV-0001", "USD", 500m, Today.AddDays(-9), paid: 100m),
            Invoice("INV-0002", "USD", 200m, Today.AddDays(-2)),
            Invoice("INV-0003", "USD", 300m, Today),
        ]);

        Calc(source).Overdue.ShouldBe(new OverdueFigure(600m, 2, 9));
    }

    [Fact]
    public void R38_plan_invoices_contribute_only_their_overdue_instalments()
    {
        var plan = new[]
        {
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(-30), 300m),
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(-4), 300m),
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(3), 400m),
        };
        var source = Source([Invoice("INV-0150", "USD", 1000m, Today.AddDays(3), paid: 300m, plan: plan)]);

        var figures = Calc(source);

        figures.Overdue.ShouldBe(new OverdueFigure(300m, 1, 4));
        figures.Outstanding.ShouldBe(new MoneyCount(700m, 1));
        figures.DueThisWeek.ShouldBe(new MoneyCount(400m, 1));
        var item = figures.Attention.Single(a => a.Kind == AttentionKind.OverdueInstalment);
        item.Text.ShouldStartWith("INV-0150 · instalment 2 of 3 is 4 days late");
    }

    [Fact]
    public void Instalments_due_beyond_seven_days_are_not_due_this_week()
    {
        var plan = new[]
        {
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(7), 100m),
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(8), 100m),
        };

        Calc(Source([Invoice("INV-0001", "USD", 200m, Today.AddDays(8), plan: plan)])).DueThisWeek.ShouldBe(new MoneyCount(100m, 1));
    }

    [Fact]
    public void Collected_compares_with_last_month_and_shows_dash_when_last_month_was_zero()
    {
        var withPrevious = Calc(Source(payments: [Paid(2026, 11, 1, "USD", 150m), Paid(2026, 10, 15, "USD", 100m)]));
        withPrevious.Collected.ShouldBe(new CollectedFigure(150m, 100m, "October", 50));

        var withoutPrevious = Calc(Source(payments: [Paid(2026, 11, 1, "USD", 150m)]));
        withoutPrevious.Collected.ChangePercent.ShouldBeNull();
    }

    [Fact]
    public void Chart_has_six_months_ending_with_the_current_one()
    {
        var bars = Calc(Source(payments: [Paid(2026, 6, 30, "USD", 10m), Paid(2026, 11, 2, "USD", 20m), Paid(2026, 5, 31, "USD", 99m)])).CollectedByMonth;

        bars.Select(b => b.Label).ShouldBe(["Jun", "Jul", "Aug", "Sep", "Oct", "Nov"]);
        bars[0].Amount.ShouldBe(10m);
        bars[^1].Amount.ShouldBe(20m);
        bars.Count(b => b.IsCurrent).ShouldBe(1);
        bars[^1].IsCurrent.ShouldBeTrue();
    }

    [Fact]
    public void Quotes_awaiting_reply_exclude_accepted_and_lapsed_quotes()
    {
        var source = Source(quotes: [
            Quote("QUO-0001", "USD", 100m, DocumentStatus.Sent, Today),
            Quote("QUO-0002", "USD", 200m, DocumentStatus.Viewed, null),
            Quote("QUO-0003", "USD", 400m, DocumentStatus.Issued, Today.AddDays(-1)),
            Quote("QUO-0004", "USD", 800m, DocumentStatus.Accepted, Today, "Tendai"),
        ]);

        Calc(source).QuotesAwaiting.ShouldBe(new MoneyCount(300m, 2));
    }

    [Fact]
    public void Needs_attention_lists_overdue_first_then_due_this_week_accepted_quotes_and_recurring_runs()
    {
        var plan = new[] { new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(2), 50m) };
        var source = Source(
            [Invoice("INV-0009", "USD", 120m, Today.AddDays(-3)), Invoice("INV-0010", "ZWG", 50m, Today.AddDays(2), plan: plan)],
            [Quote("QUO-0001", "USD", 250m, DocumentStatus.Accepted, Today.AddDays(10), "Masimba Masvosve")],
            runs: [new DashboardRecurringRun(Guid.NewGuid(), Today.AddDays(4), "Mwenga Apparel", false)]);

        var items = Calc(source).Attention;

        items.Select(i => i.Kind).ShouldBe([AttentionKind.OverdueInvoice, AttentionKind.InstalmentsDueThisWeek, AttentionKind.AcceptedQuote, AttentionKind.UpcomingRecurring]);
        items[0].Text.ShouldBe("INV-0009 · Harare Builders Co. · 3 days late · $120.00 due");
        items[1].Text.ShouldBe("1 instalment due this week · ZiG 50.00");
        items[2].Text.ShouldBe("QUO-0001 for $250.00 was accepted by Masimba Masvosve");
        items[3].Text.ShouldBe("Recurring invoice for Mwenga Apparel runs 27 Nov and creates a draft");
    }

    [Fact]
    public void Empty_business_has_zero_figures_and_nothing_needing_attention()
    {
        var figures = Calc(Source());

        figures.Outstanding.ShouldBe(new MoneyCount(0m, 0));
        figures.Overdue.ShouldBe(new OverdueFigure(0m, 0, null));
        figures.Attention.ShouldBeEmpty();
        figures.CollectedByMonth.Count.ShouldBe(6);
    }

    [Theory]
    [InlineData("Masimba Masvosve", "Masimba")]
    [InlineData("owner@troika.co.zw", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Greeting_uses_the_first_name_and_never_an_email(string? displayName, string? expected) =>
        GetDashboardHandler.FirstName(displayName).ShouldBe(expected);
}
