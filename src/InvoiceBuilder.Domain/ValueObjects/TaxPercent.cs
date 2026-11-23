using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

/// <summary>A tax percentage between 0 and 100 with four decimals (R10).</summary>
public sealed record TaxPercent
{
    private TaxPercent(decimal value) => Value = value;

    public static TaxPercent Zero { get; } = new(0m);

    public decimal Value { get; }

    public static Result<TaxPercent> Create(decimal value) =>
        value is < 0m or > 100m
            ? Result.Failure<TaxPercent>(new Error("TaxPercent.OutOfRange", "A tax percentage must be between 0 and 100."))
            : Result.Success(new TaxPercent(Math.Round(value, 4, MidpointRounding.AwayFromZero)));
}
