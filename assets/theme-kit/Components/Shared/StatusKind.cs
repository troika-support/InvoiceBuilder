namespace InvoiceBuilder.Web.Components.Shared;

/// <summary>UI status vocabulary (design-system §5.3). Map Domain DocumentStatus/PaymentStatus to this in UI services.</summary>
public enum StatusKind
{
    Draft,
    Sent,
    Viewed,
    Accepted,
    Declined,
    Expired,
    PartPaid,
    Paid,
    Overdue,
    Pending,
    Confirmed,
    Failed,
    Void,
}
