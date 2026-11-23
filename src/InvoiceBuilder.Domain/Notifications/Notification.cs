using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Notifications;

public enum NotificationKind
{
    QuoteAccepted = 0,
    QuoteDeclined = 1,
    PaymentConfirmed = 2,
    PaymentFailed = 3,
    RecurringDraftsReady = 4,
}

/// <summary>An owner-facing "Needs attention" item (spec 4.3). Stays listed until the owner dismisses it.</summary>
public sealed class Notification : BusinessEntity
{
    private Notification()
    {
    }

    public NotificationKind Kind { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    /// <summary>App-relative link, for example /invoices/{id}.</summary>
    public string? LinkPath { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    public bool IsDismissed => DismissedAt is not null;

    public static Notification Create(Guid tenantId, Guid businessId, NotificationKind kind, string title, string message, string? linkPath, DateTimeOffset now)
    {
        var notification = new Notification
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Kind = kind,
            Title = title,
            Message = message,
            LinkPath = linkPath,
        };
        notification.Initialise(now);
        return notification;
    }

    /// <summary>Idempotent: dismissing twice keeps the first time.</summary>
    public void Dismiss(DateTimeOffset now)
    {
        if (DismissedAt is not null)
        {
            return;
        }

        DismissedAt = now;
        Touch(now);
    }
}
