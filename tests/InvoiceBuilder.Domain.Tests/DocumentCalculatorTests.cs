using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

public sealed class DocumentCalculatorTests
{
    private static LineCalcInput L(decimal quantity, decimal price, decimal taxPercent = 0m) =>
        new(quantity, price, 0m, taxPercent);

    [Fact]
    public void R10_line_amount_rounds_midpoint_away_from_zero() =>
        DocumentCalculator.LineAmount(3m, 33.335m).ShouldBe(100.01m);

    [Fact]
    public void R10_line_amount_rounds_to_two_decimals() =>
        DocumentCalculator.LineAmount(0.333m, 3.00m).ShouldBe(1.00m);

    [Fact]
    public void R14_percentage_discount_is_pro_rated_across_lines()
    {
        var totals = DocumentCalculator.Calculate([L(1m, 100m), L(1m, 50m)], Discount.OfPercent(10m).Value).Value;

        totals.Subtotal.ShouldBe(150.00m);
        totals.DiscountTotal.ShouldBe(15.00m);
        totals.Total.ShouldBe(135.00m);
        totals.Lines[0].AllocatedDiscount.ShouldBe(10.00m);
        totals.Lines[1].AllocatedDiscount.ShouldBe(5.00m);
    }

    [Fact]
    public void R14_fixed_discount_is_pro_rated_across_lines()
    {
        var totals = DocumentCalculator.Calculate([L(1m, 100m), L(1m, 50m)], Discount.OfFixed(15m).Value).Value;

        totals.Lines[0].AllocatedDiscount.ShouldBe(10.00m);
        totals.Lines[1].AllocatedDiscount.ShouldBe(5.00m);
        totals.Total.ShouldBe(135.00m);
    }

    [Fact]
    public void R14_fixed_discount_remainder_goes_to_the_last_line()
    {
        var totals = DocumentCalculator.Calculate([L(1m, 10m), L(1m, 10m), L(1m, 10m)], Discount.OfFixed(10m).Value).Value;

        totals.Lines.Sum(l => l.AllocatedDiscount).ShouldBe(10.00m);
        totals.Lines[2].AllocatedDiscount.ShouldBe(3.34m);
    }

    [Fact]
    public void R14_fixed_discount_above_the_document_total_fails() =>
        DocumentCalculator.Calculate([L(1m, 10m)], Discount.OfFixed(11m).Value).IsFailure.ShouldBeTrue();

    [Fact]
    public void R13_tax_is_computed_on_the_discounted_line_amount()
    {
        var totals = DocumentCalculator.Calculate([L(1m, 100m, 15m)], Discount.OfPercent(10m).Value).Value;

        totals.TaxTotal.ShouldBe(13.50m);
        totals.Total.ShouldBe(103.50m);
    }

    [Fact]
    public void R13_tax_is_computed_per_line_then_summed()
    {
        var totals = DocumentCalculator.Calculate([L(1m, 0.10m, 15m), L(1m, 0.10m, 15m)], Discount.None).Value;

        totals.TaxTotal.ShouldBe(0.04m);
    }

    [Fact]
    public void R13_tax_snapshot_is_kept_after_the_rate_changes()
    {
        var rate = TestData.Vat(15m);
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m, rate), TestData.Now).IsSuccess.ShouldBeTrue();

        rate.ChangePercent(TaxPercent.Create(20m).Value, TestData.Now);

        invoice.Lines[0].TaxPercentSnapshot.ShouldBe(15m);
        invoice.TaxTotal.ShouldBe(15.00m);
    }

    [Fact]
    public void R9_due_date_is_issue_date_plus_terms() =>
        DocumentCalculator.DueDate(new DateOnly(2026, 10, 23), 14).ShouldBe(new DateOnly(2026, 11, 6));
}
