using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>One instalment as proposed or stored: amount, due kind and (resolved) date (R34, R35).</summary>
public sealed record InstalmentDraft(InstalmentDueKind DueKind, DateOnly? DueDate, decimal Amount, string? Note = null);

/// <summary>Derived state of one instalment (R37). Never stored.</summary>
public sealed record InstalmentState(
    int Position,
    InstalmentDueKind DueKind,
    DateOnly? DueDate,
    decimal Amount,
    decimal EffectiveAmount,
    decimal Paid,
    decimal Remaining,
    InstalmentStatus Status);

/// <summary>An instalment inside the Document aggregate.</summary>
public sealed class Instalment : BusinessEntity
{
    private Instalment()
    {
    }

    public Guid DocumentId { get; private set; }

    public int Position { get; private set; }

    public InstalmentDueKind DueKind { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    internal static Instalment Create(Document document, int position, InstalmentDraft draft, DateTimeOffset now)
    {
        var instalment = new Instalment
        {
            TenantId = document.TenantId,
            BusinessId = document.BusinessId,
            DocumentId = document.Id,
            Position = position,
            DueKind = draft.DueKind,
            DueDate = draft.DueDate,
            Amount = draft.Amount,
            Note = draft.Note,
        };
        instalment.Initialise(now);
        return instalment;
    }

    public InstalmentDraft ToDraft() => new(DueKind, DueDate, Amount, Note);

    internal void ResolveDueDate(DateOnly date, DateTimeOffset now)
    {
        DueDate = date;
        Touch(now);
    }

    internal void Replace(InstalmentDraft draft, DateTimeOffset now)
    {
        DueKind = draft.DueKind;
        DueDate = draft.DueDate;
        Amount = draft.Amount;
        Note = draft.Note;
        Touch(now);
    }
}
