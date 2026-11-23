using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

public enum DiscountKind
{
    None = 0,
    Percent = 1,
    Fixed = 2,
}

/// <summary>Document-level discount: a percentage or a fixed amount, applied before tax (R14).</summary>
public sealed record Discount
{
    private Discount(DiscountKind kind, decimal value)
    {
        Kind = kind;
        Value = value;
    }

    public static Discount None { get; } = new(DiscountKind.None, 0m);

    public DiscountKind Kind { get; }

    public decimal Value { get; }

    public static Result<Discount> OfPercent(decimal percent) =>
        percent is < 0m or > 100m
            ? Result.Failure<Discount>(new Error("Discount.PercentOutOfRange", "A percentage discount must be between 0 and 100."))
            : Result.Success(new Discount(DiscountKind.Percent, percent));

    public static Result<Discount> OfFixed(decimal amount) =>
        amount < 0m
            ? Result.Failure<Discount>(new Error("Discount.Negative", "A fixed discount cannot be negative."))
            : Result.Success(new Discount(DiscountKind.Fixed, amount));
}
