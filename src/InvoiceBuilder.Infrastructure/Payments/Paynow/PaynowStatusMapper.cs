using InvoiceBuilder.Application.Payments;

namespace InvoiceBuilder.Infrastructure.Payments.Paynow;

public readonly record struct MappedStatus(GatewayState State, bool FlagForReview);

/// <summary>
/// The status list from Paynow's "Status Update" page: Paid, Awaiting Delivery and Delivered are successful payments;
/// Created and Sent are unpaid; Cancelled is final; Disputed and Refunded need the owner. Unknown strings stay
/// Pending so nothing is confirmed by accident.
/// </summary>
public static class PaynowStatusMapper
{
    public static MappedStatus Map(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "paid" or "awaiting delivery" or "delivered" => new MappedStatus(GatewayState.Paid, false),
            "created" or "sent" => new MappedStatus(GatewayState.Pending, false),
            "cancelled" => new MappedStatus(GatewayState.Cancelled, false),
            "failed" => new MappedStatus(GatewayState.Failed, false),
            "disputed" or "refunded" => new MappedStatus(GatewayState.Failed, true),
            _ => new MappedStatus(GatewayState.Pending, false),
        };
}
