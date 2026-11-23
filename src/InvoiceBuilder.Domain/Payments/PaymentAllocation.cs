using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Payments;

public sealed class PaymentAllocation : BusinessEntity
{
    private PaymentAllocation()
    {
    }

    public Guid PaymentId { get; private set; }

    public Guid DocumentId { get; private set; }

    public decimal Amount { get; private set; }

    internal static PaymentAllocation Create(Payment payment, Guid documentId, decimal amount, DateTimeOffset now)
    {
        var allocation = new PaymentAllocation
        {
            TenantId = payment.TenantId,
            BusinessId = payment.BusinessId,
            PaymentId = payment.Id,
            DocumentId = documentId,
            Amount = amount,
        };
        allocation.Initialise(now);
        return allocation;
    }
}
