using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Businesses;

/// <summary>Gap-free per business per document type; allocated inside the issuing transaction (R6).</summary>
public sealed class NumberSequence : BusinessEntity
{
    private NumberSequence()
    {
    }

    public DocumentType DocumentType { get; private set; }

    public string Prefix { get; private set; } = string.Empty;

    public long NextValue { get; private set; } = 1;

    public int Padding { get; private set; } = 4;

    public static NumberSequence CreateDefault(Guid tenantId, Guid businessId, DocumentType type, DateTimeOffset now) =>
        Create(tenantId, businessId, type, DefaultPrefix(type), 4, now);

    public static NumberSequence Create(Guid tenantId, Guid businessId, DocumentType type, string prefix, int padding, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(padding, 1);

        var sequence = new NumberSequence
        {
            TenantId = tenantId,
            BusinessId = businessId,
            DocumentType = type,
            Prefix = prefix,
            Padding = padding,
        };
        sequence.Initialise(now);
        return sequence;
    }

    public static string DefaultPrefix(DocumentType type) => type switch
    {
        DocumentType.Quote => "QUO-",
        DocumentType.Invoice => "INV-",
        DocumentType.Receipt => "RCT-",
        DocumentType.CreditNote => "CRN-",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public Result<DocumentNumber> Allocate(DateTimeOffset now)
    {
        var number = DocumentNumber.Create(Prefix, NextValue, Padding);
        if (number.IsFailure)
        {
            return number;
        }

        NextValue++;
        Touch(now);
        return number;
    }

    public const int MaxPrefixLength = 20;

    /// <summary>Changes the prefix for future numbers only; numbers already issued keep theirs (R6).</summary>
    public Result ChangePrefix(string prefix, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > MaxPrefixLength || prefix.Any(char.IsWhiteSpace))
        {
            return Result.Failure(new Error("NumberSequence.InvalidPrefix", $"Use 1 to {MaxPrefixLength} characters without spaces."));
        }

        Prefix = prefix;
        Touch(now);
        return Result.Success();
    }

    /// <summary>Continues numbering from a previous system.
    public Result ContinueFrom(long nextValue, DateTimeOffset now)
    {
        if (nextValue < NextValue)
        {
            return Result.Failure(new Error("NumberSequence.CannotGoBack", "The next number cannot be lower than the current one."));
        }

        NextValue = nextValue;
        Touch(now);
        return Result.Success();
    }
}
