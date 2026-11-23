[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace", Target = "~N:InvoiceBuilder.Web.Components.Shared", Justification = "Folder name is mandated by the theme kit (prompt 09).")]

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
