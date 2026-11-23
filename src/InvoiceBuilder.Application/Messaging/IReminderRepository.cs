using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Messaging;

namespace InvoiceBuilder.Application.Messaging;

public sealed record ReminderBusiness(Guid TenantId, Guid BusinessId, ReminderRules Rules);

public interface IReminderRepository
{
    /// <summary>Reviewed admin query (R30, R31): the daily job has no session, so query filters are skipped to list businesses with reminders on.</summary>
    Task<IReadOnlyList<ReminderBusiness>> ListBusinessesAsync(CancellationToken cancellationToken = default);

    /// <summary>Scoped by the current tenant and business: issued, unpaid, unpaused invoices with their instalments.</summary>
    Task<IReadOnlyList<Document>> ListOpenInvoicesAsync(CancellationToken cancellationToken = default);

    Task<bool> HasLogAsync(Guid documentId, DateOnly sentOn, CancellationToken cancellationToken = default);

    void Add(ReminderLog log);
}
