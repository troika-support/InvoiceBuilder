using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Messaging;

public enum MessageChannel
{
    Email = 0,
    WhatsApp = 1,
}

public enum MessageStatus
{
    Queued = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4,
}

/// <summary>One send attempt on one channel (spec section 7). Never holds the client link or message body.</summary>
public sealed class OutboundMessage : BusinessEntity
{
    private OutboundMessage()
    {
    }

    public Guid? DocumentId { get; private set; }

    public MessageChannel Channel { get; private set; }

    public string To { get; private set; } = string.Empty;

    public string TemplateName { get; private set; } = string.Empty;

    public MessageStatus Status { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static OutboundMessage Queue(Guid tenantId, Guid businessId, Guid? documentId, MessageChannel channel, string to, string templateName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        var message = new OutboundMessage
        {
            TenantId = tenantId,
            BusinessId = businessId,
            DocumentId = documentId,
            Channel = channel,
            To = to.Trim(),
            TemplateName = templateName,
            Status = MessageStatus.Queued,
        };
        message.Initialise(now);
        return message;
    }

    public void MarkSent(string? providerMessageId, DateTimeOffset now)
    {
        Status = MessageStatus.Sent;
        ProviderMessageId = providerMessageId;
        Error = null;
        SentAt = now;
        Touch(now);
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Status = MessageStatus.Failed;
        Error = error.Length > 500 ? error[..500] : error;
        Touch(now);
    }

    /// <summary>Applies a provider delivery receipt. Status only moves forward (Sent, Delivered, Read), never back.</summary>
    public void ApplyDelivery(MessageStatus status, string? error, DateTimeOffset now)
    {
        if (status == MessageStatus.Failed)
        {
            MarkFailed(error ?? "Delivery failed.", now);
            return;
        }

        if (status > Status || Status == MessageStatus.Failed)
        {
            Status = status;
            Touch(now);
        }
    }
}
