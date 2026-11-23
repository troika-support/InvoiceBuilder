using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void R11_adding_money_with_different_currencies_fails()
    {
        var usd = new Money(10m, CurrencyCode.Usd);
        var zwg = new Money(10m, CurrencyCode.Zwg);

        var result = usd.Add(zwg);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.CurrencyMismatch");
    }

    [Fact]
    public void R11_adding_money_with_the_same_currency_sums_amounts()
    {
        var result = new Money(10.50m, CurrencyCode.Usd).Add(new Money(2.25m, CurrencyCode.Usd));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(12.75m);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("ZWG")]
    public void R11_only_iso_codes_usd_and_zwg_are_accepted(string code) =>
        CurrencyCode.Create(code).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("ZiG")]
    [InlineData("EUR")]
    [InlineData("")]
    public void R11_zig_and_other_codes_are_rejected(string code) =>
        CurrencyCode.Create(code).IsFailure.ShouldBeTrue();

    [Fact]
    public void R11_zwg_displays_as_zig() => CurrencyCode.Zwg.DisplayText.ShouldBe("ZiG");

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("1234", true)]
    [InlineData("123456789", false)]
    [InlineData("12AB", false)]
    public void R13a_hs_code_is_up_to_eight_digits(string value, bool valid) =>
        HsCode.Create(value).IsSuccess.ShouldBe(valid);

    [Fact]
    public void R6_document_number_is_prefix_plus_zero_padded_value() =>
        DocumentNumber.Create("INV-", 7, 4).Value.Value.ShouldBe("INV-0007");
}
