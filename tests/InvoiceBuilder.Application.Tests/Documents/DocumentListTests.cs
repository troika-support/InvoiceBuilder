using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Documents;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class DocumentListTests
{
    private static readonly DateOnly Today = new(2026, 6, 20);

    [Fact]
    public void R4_plain_invoice_past_due_with_balance_is_overdue_by_days()
    {
        OverdueCalculator.DaysLate(Today.AddDays(-9), 100m, 0m, 0m, [], Today).ShouldBe(9);
    }

    [Fact]
    public void R4_plain_invoice_due_today_is_not_overdue()
    {
        OverdueCalculator.DaysLate(Today, 100m, 0m, 0m, [], Today).ShouldBeNull();
    }

    [Fact]
    public void R4_fully_paid_or_credited_invoice_is_not_overdue()
    {
        OverdueCalculator.DaysLate(Today.AddDays(-9), 100m, 100m, 0m, [], Today).ShouldBeNull();
        OverdueCalculator.DaysLate(Today.AddDays(-9), 100m, 40m, 60m, [], Today).ShouldBeNull();
    }

    [Fact]
    public void R38_plan_is_overdue_only_when_an_instalment_is_overdue()
    {
        var plan = new[] { new OverdueInstalment(Today.AddDays(-30), 50m), new OverdueInstalment(Today.AddDays(30), 50m) };

        OverdueCalculator.DaysLate(Today.AddDays(30), 100m, 0m, 0m, plan, Today).ShouldBe(30);
        OverdueCalculator.DaysLate(Today.AddDays(30), 100m, 50m, 0m, plan, Today).ShouldBeNull();
    }

    [Fact]
    public void R37_credit_reduces_instalments_from_the_last_backwards()
    {
        var plan = new[] { new OverdueInstalment(Today.AddDays(-5), 50m), new OverdueInstalment(Today.AddDays(-2), 50m) };

        OverdueCalculator.DaysLate(Today.AddDays(-2), 100m, 0m, 50m, plan, Today).ShouldBe(5);
        OverdueCalculator.DaysLate(Today.AddDays(-2), 100m, 50m, 50m, plan, Today).ShouldBeNull();
    }

    [Fact]
    public void R12_csv_keeps_each_currency_in_its_own_row_and_flags_overdue()
    {
        var rows = new[]
        {
            new DocumentListRow(Guid.NewGuid(), DocumentType.Invoice, "INV-1", "Acme", Today, Today, "USD", 100m, 100m, DocumentStatus.Issued, 3),
            new DocumentListRow(Guid.NewGuid(), DocumentType.Invoice, "INV-2", "Beta, Ltd", Today, Today, "ZWG", 2500m, 0m, DocumentStatus.Paid, null),
        };

        var csv = DocumentListCsv.Build(rows);

        csv.ShouldContain("INV-1,Acme,Invoice,2026-06-20,2026-06-20,USD,100.00,100.00,Overdue (3 days late)");
        csv.ShouldContain("INV-2,\"Beta, Ltd\",Invoice,2026-06-20,2026-06-20,ZWG,2500.00,0.00,Paid");
    }

    [Fact]
    public void R33_csv_neutralises_spreadsheet_formulas()
    {
        DocumentListCsv.Escape("=SUM(A1)").ShouldBe("'=SUM(A1)");
    }
}
