using InvoiceBuilder.Application.Formatting;

namespace InvoiceBuilder.Application.Tests;

public sealed class DisplayFormatterTests
{
    private readonly DisplayFormatter _formatter = new("Africa/Harare");

    // 2026-10-23 22:30 UTC is 00:30 on 24 Oct in Harare (UTC+2).
    private static readonly DateTimeOffset Late = new(2026, 10, 23, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public void DS52_date_is_d_MMM_yyyy_in_harare() => _formatter.Date(Late).ShouldBe("24 Oct 2026");

    [Fact]
    public void DS52_short_date_is_d_MMM() => _formatter.ShortDate(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero)).ShouldBe("30 Oct");

    [Fact]
    public void DS86_month_year_is_harare_month() => _formatter.MonthYear(new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero)).ShouldBe("November 2026");

    [Fact]
    public void DS52_time_is_24_hour_in_harare() => _formatter.Time(new DateTimeOffset(2026, 10, 23, 10, 18, 0, TimeSpan.Zero)).ShouldBe("12:18");

    [Fact]
    public void DS52_time_after_midnight_rolls_into_harare_day() => _formatter.Time(Late).ShouldBe("00:30");

    [Fact]
    public void DS52_usd_has_dollar_prefix() => _formatter.Money(2400m, "USD").ShouldBe("$2,400.00");

    [Fact]
    public void DS52_zwg_shows_as_ZiG() => _formatter.Money(18200m, "ZWG").ShouldBe("ZiG 18,200.00");

    [Fact]
    public void DS52_table_amount_has_no_currency() => _formatter.Money(1200m, "USD", showCurrency: false).ShouldBe("1,200.00");

    [Fact]
    public void DS52_money_always_has_two_decimals() => _formatter.Money(5m, "USD").ShouldBe("$5.00");

    [Fact]
    public void DS52_negative_money_keeps_sign_before_symbol() => _formatter.Money(-50.5m, "USD").ShouldBe("-$50.50");

    [Theory]
    [InlineData(9, "9 days late")]
    [InlineData(1, "1 day late")]
    [InlineData(30, "30 days late")]
    public void DS52_relative_overdue(int days, string expected) => _formatter.DaysLate(days).ShouldBe(expected);
}
