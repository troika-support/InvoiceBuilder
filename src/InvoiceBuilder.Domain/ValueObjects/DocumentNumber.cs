using System.Globalization;
using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

/// <summary>Prefix plus zero-padded sequence value, e.g. INV-0001 (R6).</summary>
public sealed record DocumentNumber
{
    private DocumentNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<DocumentNumber> Create(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<DocumentNumber>(new Error("DocumentNumber.Required", "A document number is required."))
            : Result.Success(new DocumentNumber(value.Trim()));

    public static Result<DocumentNumber> Create(string? prefix, long sequence, int padding)
    {
        if (sequence < 1 || padding is < 1 or > 12)
        {
            return Result.Failure<DocumentNumber>(new Error("DocumentNumber.Invalid", "Sequence must be positive and padding between 1 and 12."));
        }

        var digits = sequence.ToString(CultureInfo.InvariantCulture).PadLeft(padding, '0');
        return Result.Success(new DocumentNumber((prefix ?? string.Empty) + digits));
    }

    public override string ToString() => Value;
}
