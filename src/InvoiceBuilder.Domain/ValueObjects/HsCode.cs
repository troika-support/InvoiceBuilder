using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.ValueObjects;

/// <summary>Optional harmonised-system code, up to 8 digits (R13a).</summary>
public sealed record HsCode
{
    private HsCode(string value) => Value = value;

    public string Value { get; }

    public static Result<HsCode> Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var valid = trimmed.Length is >= 1 and <= 8 && trimmed.All(char.IsAsciiDigit);

        return valid
            ? Result.Success(new HsCode(trimmed))
            : Result.Failure<HsCode>(new Error("HsCode.Invalid", "An HS code is 1 to 8 digits."));
    }

    public override string ToString() => Value;
}
