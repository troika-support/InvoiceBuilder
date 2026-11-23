using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class DocumentListQueries(AppDbContext db) : IDocumentListQueries
{
    public async Task<DocumentListPage> ListAsync(DocumentListFilter filter, int page, int pageSize, DateOnly today, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var types = filter.Types.ToList();
        var ofType = db.Documents.Where(d => types.Contains(d.Type));
        var allCount = await ofType.CountAsync(cancellationToken);

        var q = ofType;
        if (filter.ClientId is { } clientId)
        {
            q = q.Where(d => d.ClientId == clientId);
        }

        if (filter.From is { } from)
        {
            q = q.Where(d => d.IssueDate >= from);
        }

        if (filter.To is { } to)
        {
            q = q.Where(d => d.IssueDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Currency))
        {
            var code = filter.Currency.Trim().ToUpperInvariant();
            q = q.Where(d => d.Currency.Code == code);
        }

        var statuses = filter.Statuses.ToList();
        if (statuses.Count > 0)
        {
            q = q.Where(d => statuses.Contains(d.Status));
        }

        var term = filter.Search?.Trim();
        var clientNames = await db.Clients.Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        if (!string.IsNullOrEmpty(term))
        {
            var matchingClients = clientNames.Where(c => c.Value.Contains(term, StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).ToList();
            q = q.Where(d => (d.Number != null && d.Number.Value.Contains(term)) || matchingClients.Contains(d.ClientId));
        }

        var docs = await q
            .Select(d => new { d.Id, d.Type, Number = d.Number != null ? d.Number.Value : null, d.ClientId, d.IssueDate, d.IssuedAt, d.DueDate, d.ValidUntil, d.Currency, d.Total, d.Status, d.CreatedAt })
            .ToListAsync(cancellationToken);

        var invoiceIds = docs.Where(d => d.Type == DocumentType.Invoice && d.Status != DocumentStatus.Draft).Select(d => d.Id).ToList();

        var paidById = invoiceIds.Count == 0
            ? []
            : (await (
                from a in db.PaymentAllocations
                join p in db.Payments on a.PaymentId equals p.Id
                where p.Status == PaymentStatus.Confirmed && invoiceIds.Contains(a.DocumentId)
                select new { a.DocumentId, a.Amount })
                .ToListAsync(cancellationToken))
                .GroupBy(a => a.DocumentId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var creditedById = invoiceIds.Count == 0
            ? []
            : (await db.Documents
                .Where(d => d.Type == DocumentType.CreditNote
                    && d.Status != DocumentStatus.Draft
                    && d.Status != DocumentStatus.Void
                    && d.SourceDocumentId != null
                    && invoiceIds.Contains(d.SourceDocumentId.Value))
                .Select(d => new { SourceId = d.SourceDocumentId!.Value, d.Total })
                .ToListAsync(cancellationToken))
                .GroupBy(c => c.SourceId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Total));

        var plansById = invoiceIds.Count == 0
            ? []
            : (await db.Instalments
                .Where(i => invoiceIds.Contains(i.DocumentId) && i.DueDate != null)
                .Select(i => new { i.DocumentId, DueDate = i.DueDate!.Value, i.Amount })
                .ToListAsync(cancellationToken))
                .GroupBy(i => i.DocumentId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<OverdueInstalment>)g.Select(i => new OverdueInstalment(i.DueDate, i.Amount)).ToList());

        var rows = docs.Select(d =>
        {
            var issued = d.IssueDate ?? (d.IssuedAt is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : (DateOnly?)null);
            var isInvoice = d.Type == DocumentType.Invoice && d.Status != DocumentStatus.Draft;
            var paid = paidById.GetValueOrDefault(d.Id);
            var credited = creditedById.GetValueOrDefault(d.Id);
            var open = isInvoice && d.Status is DocumentStatus.Issued or DocumentStatus.Sent or DocumentStatus.Viewed or DocumentStatus.PartPaid;
            decimal? balance = isInvoice ? Math.Max(0m, d.Total - paid - credited) : null;
            int? late = open ? OverdueCalculator.DaysLate(d.DueDate, d.Total, paid, credited, plansById.GetValueOrDefault(d.Id) ?? [], today) : null;
            return new DocumentListRow(
                d.Id,
                d.Type,
                d.Number,
                clientNames.GetValueOrDefault(d.ClientId, string.Empty),
                issued,
                d.Type == DocumentType.Quote ? d.ValidUntil : d.DueDate,
                d.Currency.Code,
                d.Total,
                balance,
                d.Status,
                late,
                d.ClientId);
        });

        if (filter.OverdueOnly)
        {
            rows = rows.Where(r => r.DaysLate is not null);
        }

        var ordered = (filter.Sort, filter.Descending) switch
        {
            (DocumentListSort.Number, false) => rows.OrderBy(r => r.Number, StringComparer.OrdinalIgnoreCase),
            (DocumentListSort.Number, true) => rows.OrderByDescending(r => r.Number, StringComparer.OrdinalIgnoreCase),
            (DocumentListSort.Client, false) => rows.OrderBy(r => r.ClientName, StringComparer.OrdinalIgnoreCase),
            (DocumentListSort.Client, true) => rows.OrderByDescending(r => r.ClientName, StringComparer.OrdinalIgnoreCase),
            (DocumentListSort.Amount, false) => rows.OrderBy(r => r.Total),
            (DocumentListSort.Amount, true) => rows.OrderByDescending(r => r.Total),
            (DocumentListSort.Status, false) => rows.OrderBy(r => r.DaysLate is not null ? -1 : (int)r.Status),
            (DocumentListSort.Status, true) => rows.OrderByDescending(r => r.DaysLate is not null ? -1 : (int)r.Status),
            (_, false) => rows.OrderBy(r => r.IssueDate ?? DateOnly.MaxValue),
            _ => rows.OrderByDescending(r => r.IssueDate ?? DateOnly.MaxValue),
        };

        var all = ordered.ToList();
        var size = Math.Max(1, pageSize);
        var items = all.Skip((Math.Max(1, page) - 1) * size).Take(size).ToList();
        return new DocumentListPage(items, all.Count, allCount);
    }
}
