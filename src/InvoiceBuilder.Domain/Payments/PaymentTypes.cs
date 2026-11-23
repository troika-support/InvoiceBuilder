using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Payments;

public enum PaymentMethod
{
    EcoCash = 0,
    OneMoney = 1,
    InnBucks = 2,
    Card = 3,
    BankTransfer = 4,
    Cash = 5,
    Other = 6,
}

public enum PaymentStatus
{
    Pending = 0,
    Confirmed = 1,
    Failed = 2,
}

public sealed record PaymentConfirmed(
    Guid PaymentId,
    Guid BusinessId,
    Guid ClientId,
    decimal Amount,
    CurrencyCode Currency,
    DateTimeOffset OccurredAt) : IDomainEvent;
