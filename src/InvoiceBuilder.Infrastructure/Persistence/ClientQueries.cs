using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class ClientQueries(AppDbContext db) : IClientQueries
{
    public async Task<ClientPage> ListAsync(string? search, ClientFilter filter, int page, int pageSize, DateOnly today, CancellationToken cancellationToken = default)
    {
        var active = db.Clients.Where(c => !c.IsArchived);
        var allCount = await active.CountAsync(cancellationToken);

        var term = search?.Trim();
        var matching = string.IsNullOrEmpty(term)
            ? active
            : active.Where(c => c.Name.Contains(term)
                || (c.Email != null && c.Email.Contains(term))
                || (c.Phone != null && c.Phone.Contains(term))
                || (c.WhatsApp != null && c.WhatsApp.Contains(term)));

        var clients = await matching
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Email, c.Phone })
            .ToListAsync(cancellationToken);

        var ids = clients.Select(c => c.Id).ToList();
        var facts = await LoadFactsAsync(ids, cancellationToken);

        var rows = clients
            .Select(c =>
            {
                var invoices = facts.TryGetValue(c.Id, out var f) ? f : [];
                var balances = ClientMetrics.BalancesByCurrency(invoices.Select(i => i.Facts));
                var overdue = ClientMetrics.OverdueByCurrency(invoices.Select(i => i.Facts), today);
                DateOnly? last = invoices.Count == 0 ? null : invoices.Max(i => i.Facts.IssueDate);
                var billed = ClientMetrics.BilledInYear(invoices.Select(i => i.Facts), today.Year);
                return (Item: new ClientListItem(c.Id, c.Name, c.Email, last, balances, billed, c.Phone), HasOverdue: overdue.Count > 0);
            })
            .Where(r => filter switch
            {
                ClientFilter.WithBalanceDue => r.Item.Balances.Count > 0,
                ClientFilter.Overdue => r.HasOverdue,
                _ => true,
            })
            .Select(r => r.Item)
            .ToList();

        var pageSizeSafe = Math.Max(1, pageSize);
        var items = rows.Skip((Math.Max(1, page) - 1) * pageSizeSafe).Take(pageSizeSafe).ToList();
        return new ClientPage(items, rows.Count, allCount);
    }

    public async Task<ClientDetail?> GetDetailAsync(Guid clientId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
        {
            return null;
        }

        var facts = await LoadFactsAsync([clientId], cancellationToken);
        var invoices = (facts.TryGetValue(clientId, out var f) ? f : []).Select(i => i.Facts).ToList();

        var documents = await db.Documents
            .Where(d => d.ClientId == clientId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        var activity = await db.ActivityEntries
            .Where(a => a.EntityType == "Client" && a.EntityId == clientId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(20)
            .Select(a => new ClientActivityRow(a.OccurredAt, a.Summary))
            .ToListAsync(cancellationToken);

        return new ClientDetail(
            client.Id,
            client.Name,
            client.Email,
            client.Phone,
            client.WhatsApp,
            client.Address,
            client.DefaultCurrency.Code,
            client.Notes,
            client.CreatedAt,
            ClientMetrics.BalancesByCurrency(invoices),
            ClientMetrics.OverdueByCurrency(invoices, today),
            ClientMetrics.BilledInYear(invoices, today.Year),
            ClientMetrics.AverageDaysToPay(invoices),
            documents
                .Select(d => new ClientDocumentRow(d.Id, d.Number?.Value, d.Type.ToString(), d.Status.ToString(), d.Currency.Code, d.Total, d.IssueDate))
                .ToList(),
            activity);
    }

    public Task<bool> NameExistsAsync(string name, Guid? exceptClientId, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return db.Clients.AnyAsync(c => c.Name == trimmed && (exceptClientId == null || c.Id != exceptClientId), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken = default) =>
        await db.Clients.Select(c => c.Name).ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, List<ClientInvoice>>> LoadFactsAsync(List<Guid> clientIds, CancellationToken cancellationToken)
    {
        var invoices = await db.Documents
            .Where(d => d.Type == DocumentType.Invoice && d.Status != DocumentStatus.Draft && clientIds.Contains(d.ClientId))
            .Select(d => new { d.Id, d.ClientId, d.Currency, d.Total, d.Status, d.IssueDate, d.IssuedAt, d.DueDate })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return [];
        }

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        var allocations = await (
            from a in db.PaymentAllocations
            join p in db.Payments on a.PaymentId equals p.Id
            where p.Status == PaymentStatus.Confirmed && invoiceIds.Contains(a.DocumentId)
            select new { a.DocumentId, a.Amount, p.PaidAt })
            .ToListAsync(cancellationToken);

        var credits = await db.Documents
            .Where(d => d.Type == DocumentType.CreditNote
                && d.Status != DocumentStatus.Draft
                && d.Status != DocumentStatus.Void
                && d.SourceDocumentId != null
                && invoiceIds.Contains(d.SourceDocumentId.Value))
            .Select(d => new { SourceId = d.SourceDocumentId!.Value, d.Total })
            .ToListAsync(cancellationToken);

        return invoices
            .GroupBy(i => i.ClientId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i =>
                {
                    var paid = allocations.Where(a => a.DocumentId == i.Id).ToList();
                    DateOnly? paidOn = paid.Count == 0 ? null : DateOnly.FromDateTime(paid.Max(a => a.PaidAt).UtcDateTime);
                    var issued = i.IssueDate ?? (i.IssuedAt is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : DateOnly.FromDateTime(DateTime.UtcNow));
                    var facts = new InvoiceFacts(
                        i.Currency.Code,
                        i.Total,
                        i.Status,
                        paid.Sum(a => a.Amount),
                        credits.Where(c => c.SourceId == i.Id).Sum(c => c.Total),
                        issued,
                        i.DueDate,
                        paidOn);
                    return new ClientInvoice(facts);
                }).ToList());
    }

    private sealed record ClientInvoice(InvoiceFacts Facts);
}
