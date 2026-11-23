namespace InvoiceBuilder.Domain.Common;

public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error None { get; } = new(string.Empty, string.Empty);
}
