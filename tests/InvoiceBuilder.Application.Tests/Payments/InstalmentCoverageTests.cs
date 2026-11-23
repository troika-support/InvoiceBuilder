using InvoiceBuilder.Application.Payments;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class InstalmentCoverageTests
{
    private static string Money(decimal v) => "$" + v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void R37_1000_on_3x800_covers_instalment_1_and_200_of_instalment_2()
    {
        InstalmentCoverage.Describe([800m, 800m, 800m], 0m, 1000m, Money).ShouldBe("Covers instalment 1 and $200.00 of instalment 2");
    }

    [Fact]
    public void R37_second_payment_finishes_the_part_paid_instalment()
    {
        InstalmentCoverage.Describe([800m, 800m, 800m], 1000m, 600m, Money).ShouldBe("Covers the rest of instalment 2");
    }

    [Fact]
    public void R37_no_plan_gives_no_line()
    {
        InstalmentCoverage.Describe([], 0m, 100m, Money).ShouldBeNull();
    }
}
