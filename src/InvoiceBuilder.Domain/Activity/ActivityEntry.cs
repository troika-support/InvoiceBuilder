using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Activity;

/// <summary>One line of the audit trail (R33). Append-only: there is no way to change an entry after it is created.</summary>
public sealed class ActivityEntry : TenantEntity
{
    private ActivityEntry()
    {
    }

    public Guid? BusinessId { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string? ActorUserId { get; private set; }

    public string ActorLabel { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public static ActivityEntry Create(
        Guid tenantId,
        Guid? businessId,
        string entityType,
        Guid entityId,
        string action,
        string? actorUserId,
        string actorLabel,
        string summary,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorLabel);

        var entry = new ActivityEntry
        {
            TenantId = tenantId,
            BusinessId = businessId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorUserId = actorUserId,
            ActorLabel = actorLabel,
            Summary = summary ?? string.Empty,
            OccurredAt = now,
        };
        entry.Initialise(now);
        return entry;
    }
}
