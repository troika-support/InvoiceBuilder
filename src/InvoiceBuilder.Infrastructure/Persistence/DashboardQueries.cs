using System.Collections.Concurrent;
using InvoiceBuilder.Application.Dashboard;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Loads what the dashboard needs for the current business (query filters scope it). A handful of small queries.</summary>
public sealed class DashboardQueries(AppDbContext db) : IDashboardQueries
{
    private static readonly DocumentStatus[] OpenInvoice = [DocumentStatus.Issued, DocumentStatus.Sent, DocumentStatus.Viewed, DocumentStatus.PartPaid];
    private static readonly DocumentStatus[] QuoteStates = [DocumentStatus.Issued, DocumentStatus.Sent, DocumentStatus.Viewed, DocumentStatus.Accepted];

    public async Task<DashboardSource> LoadAsync(DateTimeOffset paymentsSince, DateOnly recurringUntil, CancellationToken cancellationToken = default)
    {
        var clients = await db.Clients
            .Select(c => new { c.Id, c.Name, c.Email, Phone = c.WhatsApp ?? c.Phone })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var invoiceRows = await db.Documents
            .Where(d => d.Type == DocumentType.Invoice && d.Number != null && OpenInvoice.Contains(d.Status))
            .Select(d => new { d.Id, Number = d.Number!.Value, d.ClientId, d.Currency, d.Total, d.DueDate })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoiceRows.Select(i => i.Id).ToList();

        var paid = invoiceIds.Count == 0 ? [] : (await (
                from a in db.PaymentAllocations
                join p in db.Payments on a.PaymentId equals p.Id
                where p.Status == PaymentStatus.Confirmed && invoiceIds.Contains(a.DocumentId)
                select new { a.DocumentId, a.Amount })
            .ToListAsync(cancellationToken))
            .GroupBy(a => a.DocumentId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var credited = invoiceIds.Count == 0 ? [] : (await db.Documents
                .Where(d => d.Type == DocumentType.CreditNote && d.Status != DocumentStatus.Draft && d.Status != DocumentStatus.Void
                    && d.SourceDocumentId != null && invoiceIds.Contains(d.SourceDocumentId.Value))
                .Select(d => new { SourceId = d.SourceDocumentId!.Value, d.Total })
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.SourceId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Total));

        var plans = invoiceIds.Count == 0 ? [] : (await db.Instalments
                .Where(i => invoiceIds.Contains(i.DocumentId))
                .Select(i => new { i.DocumentId, i.Position, i.DueKind, i.DueDate, i.Amount })
                .ToListAsync(cancellationToken))
            .GroupBy(i => i.DocumentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<InstalmentDraft>)g.OrderBy(i => i.Position).Select(i => new InstalmentDraft(i.DueKind, i.DueDate, i.Amount)).ToList());

        var invoices = invoiceRows.Select(i =>
        {
            var client = clients.GetValueOrDefault(i.ClientId);
            return new DashboardInvoice(
                i.Id, i.Number, i.ClientId, client?.Name ?? string.Empty, client?.Email, client?.Phone, i.Currency.Code, i.Total,
                paid.GetValueOrDefault(i.Id), credited.GetValueOrDefault(i.Id), i.DueDate, plans.GetValueOrDefault(i.Id) ?? []);
        }).ToList();

        var quoteRows = await db.Documents
            .Where(d => d.Type == DocumentType.Quote && d.Number != null && QuoteStates.Contains(d.Status))
            .Select(d => new { d.Id, Number = d.Number!.Value, d.ClientId, d.Currency, d.Total, d.Status, d.ValidUntil })
            .ToListAsync(cancellationToken);
        var acceptedIds = quoteRows.Where(q => q.Status == DocumentStatus.Accepted).Select(q => q.Id).ToList();
        var signers = acceptedIds.Count == 0 ? [] : await db.QuoteAcceptances
            .Where(a => acceptedIds.Contains(a.DocumentId) && a.Decision == QuoteDecision.Accepted)
            .GroupBy(a => a.DocumentId)
            .Select(g => new { DocumentId = g.Key, Signer = g.OrderByDescending(a => a.AcceptedAt).Select(a => a.SignerName).First() })
            .ToDictionaryAsync(a => a.DocumentId, a => a.Signer, cancellationToken);
        var quotes = quoteRows
            .Select(q => new DashboardQuote(q.Id, q.Number, clients.GetValueOrDefault(q.ClientId)?.Name ?? string.Empty, q.Currency.Code, q.Total, q.Status, q.ValidUntil, signers.GetValueOrDefault(q.Id)))
            .ToList();

        var payments = (await db.Payments
                .Where(p => p.Status == PaymentStatus.Confirmed && p.PaidAt >= paymentsSince)
                .Select(p => new { p.PaidAt, p.Currency, p.Amount })
                .ToListAsync(cancellationToken))
            .Select(p => new DashboardPayment(p.PaidAt, p.Currency.Code, p.Amount))
            .ToList();

        var schedules = await db.RecurringSchedules
            .Where(r => !r.Paused && r.NextRunDate <= recurringUntil)
            .ToListAsync(cancellationToken);
        var sourceIds = schedules.Select(s => s.SourceDocumentId).ToList();
        var sourceClients = sourceIds.Count == 0 ? [] : await db.Documents
            .Where(d => sourceIds.Contains(d.Id))
            .Select(d => new { d.Id, d.ClientId })
            .ToDictionaryAsync(d => d.Id, d => d.ClientId, cancellationToken);
        var runs = schedules
            .Where(s => !s.IsFinished)
            .Select(s => new DashboardRecurringRun(
                s.Id,
                s.NextRunDate,
                sourceClients.TryGetValue(s.SourceDocumentId, out var clientId) ? clients.GetValueOrDefault(clientId)?.Name ?? string.Empty : string.Empty,
                s.AutoIssue))
            .ToList();

        return new DashboardSource(invoices, quotes, payments, runs);
    }
}

/// <summary>
/// In-memory dashboard cache: 60 s per business and currency. Invalidating bumps the business's generation, so every
/// cached currency for it is skipped at once and simply expires.
/// </summary>
public sealed class MemoryDashboardCache(IMemoryCache cache) : IDashboardCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, long> _generations = new();

    public async Task<DashboardFigures> GetOrCreateAsync(Guid businessId, string currency, Func<CancellationToken, Task<DashboardFigures>> create, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(create);
        var key = $"dashboard:{businessId:N}:{_generations.GetValueOrDefault(businessId)}:{currency}";
        if (cache.TryGetValue(key, out DashboardFigures? cached) && cached is not null)
        {
            return cached;
        }

        var figures = await create(cancellationToken);
        cache.Set(key, figures, Lifetime);
        return figures;
    }

    public void Invalidate(Guid businessId) => _generations.AddOrUpdate(businessId, 1, (_, g) => g + 1);
}
