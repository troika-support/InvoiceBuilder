using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Payments;

/// <summary>One requested allocation against an invoice and that invoice's facts at the time.</summary>
public sealed record AllocationRequest(Guid DocumentId, Guid ClientId, CurrencyCode Currency, decimal Balance, decimal Amount);

/// <summary>Allocation (R16) and balance/status (R17) rules. Pure apart from filling the payment's allocations.</summary>
public static class PaymentAllocator
{
    public static decimal Balance(decimal total, decimal creditedTotal, decimal confirmedAllocations) =>
        total - creditedTotal - confirmedAllocations;

    public static DocumentStatus InvoiceStatusFor(DocumentStatus current, decimal total, decimal creditedTotal, decimal confirmedPaid)
    {
        if (current == DocumentStatus.Draft)
        {
            return current;
        }

        if (total > 0m && creditedTotal >= total)
        {
            return DocumentStatus.Void;
        }

        if (Balance(total, creditedTotal, confirmedPaid) <= 0m)
        {
            return DocumentStatus.Paid;
        }

        return confirmedPaid > 0m ? DocumentStatus.PartPaid : current;
    }

    /// <summary>
    /// Allocations go to invoices of the payment's client and currency; the sum cannot exceed the payment and
    /// each amount cannot exceed its invoice's balance. On success the payment's allocations are replaced.
    /// </summary>
    public static Result<IReadOnlyList<PaymentAllocation>> Allocate(Payment payment, IReadOnlyList<AllocationRequest> requests)
    {
        foreach (var request in requests)
        {
            var failure = Check(payment, request);
            if (failure is not null)
            {
                return Result.Failure<IReadOnlyList<PaymentAllocation>>(failure);
            }
        }

        if (requests.Select(r => r.DocumentId).Distinct().Count() != requests.Count)
        {
            return Result.Failure<IReadOnlyList<PaymentAllocation>>(
                new Error("Payment.DuplicateAllocation", "An invoice can be allocated only once per payment."));
        }

        if (requests.Sum(r => r.Amount) > payment.Amount)
        {
            return Result.Failure<IReadOnlyList<PaymentAllocation>>(
                new Error("Payment.AllocationExceedsPayment", "Allocations cannot add up to more than the payment."));
        }

        payment.ApplyAllocations(requests.Select(r => (r.DocumentId, r.Amount)), TimeProvider.System.GetUtcNow());
        return Result.Success(payment.Allocations);
    }

    private static Error? Check(Payment payment, AllocationRequest request)
    {
        if (request.ClientId != payment.ClientId)
        {
            return new Error("Payment.ClientMismatch", "A payment can only be allocated to its own client's invoices.");
        }

        if (request.Currency != payment.Currency)
        {
            return new Error("Payment.CurrencyMismatch", "A payment can only be allocated to invoices in its currency.");
        }

        if (request.Amount <= 0m)
        {
            return new Error("Payment.AllocationNotPositive", "Each allocation must be above zero.");
        }

        return request.Amount > request.Balance
            ? new Error("Payment.AllocationExceedsBalance", "An allocation cannot exceed the invoice balance.")
            : null;
    }
}
