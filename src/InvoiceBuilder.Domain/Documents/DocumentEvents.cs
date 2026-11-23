using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

public sealed record DocumentIssued(Guid DocumentId, Guid BusinessId, DocumentType Type, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DocumentViewed(Guid DocumentId, Guid BusinessId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record QuoteAccepted(Guid DocumentId, Guid BusinessId, string SignerName, DateTimeOffset OccurredAt) : IDomainEvent;
