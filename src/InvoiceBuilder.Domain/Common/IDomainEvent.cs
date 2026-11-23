namespace InvoiceBuilder.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
