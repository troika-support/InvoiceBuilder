using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

/// <summary>An amount in one currency. Money never mixes currencies (R11, R12).</summary>
public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public static Error CurrencyMismatch { get; } = new("Money.CurrencyMismatch", "Amounts in different currencies cannot be combined.");

    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    public Result<Money> Add(Money other) =>
        Currency != other.Currency
            ? Result.Failure<Money>(CurrencyMismatch)
            : Result.Success(new Money(Amount + other.Amount, Currency));

    public Result<Money> Subtract(Money other) =>
        Currency != other.Currency
            ? Result.Failure<Money>(CurrencyMismatch)
            : Result.Success(new Money(Amount - other.Amount, Currency));
}
