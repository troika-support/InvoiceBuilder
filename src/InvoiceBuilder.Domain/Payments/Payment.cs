using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Payments;

/// <summary>A payment. Only Confirmed payments count (R15); Paynow payments start Pending (R19).</summary>
public sealed class Payment : BusinessEntity
{
    private readonly List<PaymentAllocation> _allocations = [];

    private Payment()
    {
    }

    public Guid ClientId { get; private set; }

    public decimal Amount { get; private set; }

    public CurrencyCode Currency { get; private set; } = CurrencyCode.Usd;

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    public DateTimeOffset PaidAt { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public string? ProviderReference { get; private set; }

    public string? PollUrl { get; private set; }

    /// <summary>Set when the payment needs the owner's attention (amount mismatch, disputed or refunded).</summary>
    public string? ReviewReason { get; private set; }

    public IReadOnlyList<PaymentAllocation> Allocations => _allocations;

    public Money Money => new(Amount, Currency);

    public static Result<Payment> Create(
        Guid tenantId,
        Guid businessId,
        Guid clientId,
        decimal amount,
        CurrencyCode currency,
        PaymentMethod method,
        string reference,
        DateTimeOffset paidAt,
        DateTimeOffset now,
        string? providerReference = null,
        string? pollUrl = null)
    {
        if (amount <= 0m)
        {
            return Result.Failure<Payment>(new Error("Payment.AmountNotPositive", "A payment amount must be above zero."));
        }

        var payment = new Payment
        {
            TenantId = tenantId,
            BusinessId = businessId,
            ClientId = clientId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Reference = reference,
            PaidAt = paidAt,
            ProviderReference = providerReference,
            PollUrl = pollUrl,
        };
        payment.Initialise(now);
        return Result.Success(payment);
    }

    /// <summary>Idempotent: confirming an already confirmed payment does nothing (R19).</summary>
    public Result Confirm(DateTimeOffset now)
    {
        if (Status == PaymentStatus.Confirmed)
        {
            return Result.Success();
        }

        if (Status == PaymentStatus.Failed)
        {
            return Result.Failure(new Error("Payment.AlreadyFailed", "A failed payment cannot be confirmed."));
        }

        Status = PaymentStatus.Confirmed;
        Touch(now);
        Raise(new PaymentConfirmed(Id, BusinessId, ClientId, Amount, Currency, now));
        return Result.Success();
    }

    public Result Fail(DateTimeOffset now)
    {
        if (Status == PaymentStatus.Failed)
        {
            return Result.Success();
        }

        if (Status == PaymentStatus.Confirmed)
        {
            return Result.Failure(new Error("Payment.AlreadyConfirmed", "A confirmed payment cannot be failed."));
        }

        Status = PaymentStatus.Failed;
        Touch(now);
        return Result.Success();
    }

    public void SetProviderDetails(string? providerReference, string? pollUrl, DateTimeOffset now)
    {
        ProviderReference = providerReference ?? ProviderReference;
        PollUrl = pollUrl ?? PollUrl;
        Touch(now);
    }

    public void FlagForReview(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ReviewReason = reason.Length > 500 ? reason[..500] : reason;
        Touch(now);
    }

    internal void ApplyAllocations(IEnumerable<(Guid DocumentId, decimal Amount)> allocations, DateTimeOffset now)
    {
        _allocations.Clear();
        foreach (var (documentId, amount) in allocations)
        {
            _allocations.Add(PaymentAllocation.Create(this, documentId, amount, now));
        }

        Touch(now);
    }
}
