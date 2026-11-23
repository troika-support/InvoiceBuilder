using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

/// <summary>ISO 4217 code. Only USD and ZWG are supported; "ZiG" is display text only (R11).</summary>
public sealed record CurrencyCode
{
    private CurrencyCode(string code) => Code = code;

    public static CurrencyCode Usd { get; } = new("USD");

    public static CurrencyCode Zwg { get; } = new("ZWG");

    public string Code { get; }

    public string DisplayText => Code == "ZWG" ? "ZiG" : Code;

    public static Result<CurrencyCode> Create(string? code) =>
        code?.Trim().ToUpperInvariant() switch
        {
            "USD" => Result.Success(Usd),
            "ZWG" => Result.Success(Zwg),
            _ => Result.Failure<CurrencyCode>(new Error("Currency.Unsupported", "Currency must be USD or ZWG.")),
        };

    public override string ToString() => Code;
}
