using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Messaging;

public enum ReminderKind
{
    BeforeDue = 0,
    OnDueDate = 1,
    AfterDue = 2,
}

/// <summary>One reminder sent for an invoice on one day (R31). A unique (DocumentId, SentOn) index makes the job idempotent.</summary>
public sealed class ReminderLog : BusinessEntity
{
    private ReminderLog()
    {
    }

    public Guid DocumentId { get; private set; }

    /// <summary>The most urgent kind when one combined message covers several instalments (R39).</summary>
    public ReminderKind Kind { get; private set; }

    public DateOnly SentOn { get; private set; }

    public MessageChannel Channel { get; private set; }

    /// <summary>Comma-separated instalment positions covered, empty for a whole-invoice reminder.</summary>
    public string InstalmentPositions { get; private set; } = string.Empty;

    public static ReminderLog Create(Guid tenantId, Guid businessId, Guid documentId, ReminderKind kind, DateOnly sentOn, MessageChannel channel, IEnumerable<int> instalmentPositions, DateTimeOffset now)
    {
        var log = new ReminderLog
        {
            TenantId = tenantId,
            BusinessId = businessId,
            DocumentId = documentId,
            Kind = kind,
            SentOn = sentOn,
            Channel = channel,
            InstalmentPositions = string.Join(',', instalmentPositions),
        };
        log.Initialise(now);
        return log;
    }
}
