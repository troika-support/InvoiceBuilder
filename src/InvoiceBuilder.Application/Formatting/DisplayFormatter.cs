using System.Globalization;
using InvoiceBuilder.Application.Abstractions;

namespace InvoiceBuilder.Application.Formatting;

/// <summary>Design-system §5.2 formats. Culture-invariant so output never depends on the server's regional settings.</summary>
public sealed class DisplayFormatter(string timeZoneId) : IDisplayFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    public string Date(DateTimeOffset value) => Local(value).ToString("d MMM yyyy", Culture);

    public string ShortDate(DateTimeOffset value) => Local(value).ToString("d MMM", Culture);

    public string MonthYear(DateTimeOffset value) => Local(value).ToString("MMMM yyyy", Culture);

    public string Time(DateTimeOffset value) => Local(value).ToString("HH:mm", Culture);

    public string Money(decimal amount, string currency, bool showCurrency = true)
    {
        var number = Math.Abs(amount).ToString("#,##0.00", Culture);
        var sign = amount < 0 ? "-" : string.Empty;
        if (!showCurrency)
        {
            return sign + number;
        }

        var prefix = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "ZWG" => "ZiG ",
            var other => other + " ",
        };
        return sign + prefix + number;
    }

    public string DaysLate(int days) => days == 1 ? "1 day late" : string.Create(Culture, $"{days} days late");

    private DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, _zone);
}
