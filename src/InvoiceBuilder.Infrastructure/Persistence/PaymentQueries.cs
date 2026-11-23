using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class PaymentQueries(AppDbContext db) : IPaymentQueries
{
    public async Task<IReadOnlyList<LedgerRow>> LedgerAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        var payments = await db.Payments.Where(p => p.PaidAt >= from && p.PaidAt < until)
            .Select(p => new { p.Id, p.PaidAt, p.ClientId, p.Method, p.Amount, Currency = p.Currency.Code, p.Status })
            .ToListAsync(cancellationToken);
        var ids = payments.Select(p => p.Id).ToList();
        var applied = await (from a in db.PaymentAllocations join d in db.Documents on a.DocumentId equals d.Id where ids.Contains(a.PaymentId) select new { a.PaymentId, Number = d.Number != null ? d.Number.Value : null }).ToListAsync(cancellationToken);
        var names = await db.Clients.Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return [.. payments.OrderByDescending(p => p.PaidAt).Select(p => new LedgerRow(
            p.Id, p.PaidAt, names.GetValueOrDefault(p.ClientId, string.Empty),
            string.Join(", ", applied.Where(a => a.PaymentId == p.Id && a.Number is not null).Select(a => a.Number)),
            p.Method, p.Amount, p.Currency, p.Status))];
    }

    public async Task<IReadOnlyList<OpenInvoiceRow>> OpenInvoicesAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Documents.Where(d => d.ClientId == clientId && d.Type == DocumentType.Invoice && d.Status != DocumentStatus.Draft && d.Status != DocumentStatus.Void && d.Status != DocumentStatus.Paid)
            .Select(d => new { d.Id, Number = d.Number != null ? d.Number.Value : null, d.DueDate, d.Total, Currency = d.Currency.Code, d.IssueDate })
            .ToListAsync(cancellationToken);
        var ids = invoices.Select(d => d.Id).ToList();
        var paid = (await (from a in db.PaymentAllocations join p in db.Payments on a.PaymentId equals p.Id where p.Status == PaymentStatus.Confirmed && ids.Contains(a.DocumentId) select new { a.DocumentId, a.Amount }).ToListAsync(cancellationToken))
            .GroupBy(a => a.DocumentId).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        var credited = (await db.Documents.Where(d => d.Type == DocumentType.CreditNote && d.SourceDocumentId != null && ids.Contains(d.SourceDocumentId.Value)).Select(d => new { Id = d.SourceDocumentId!.Value, d.Total }).ToListAsync(cancellationToken))
            .GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Sum(d => d.Total));
        var plans = (await db.Instalments.Where(n => ids.Contains(n.DocumentId)).Select(n => new { n.DocumentId, n.Position, n.Amount }).ToListAsync(cancellationToken))
            .GroupBy(n => n.DocumentId).ToDictionary(g => g.Key, g => (IReadOnlyList<decimal>)[.. g.OrderBy(n => n.Position).Select(n => n.Amount)]);
        return [.. invoices.OrderBy(d => d.IssueDate).Select(d => new OpenInvoiceRow(d.Id, d.Number ?? string.Empty, d.DueDate, PaymentAllocator.Balance(d.Total, credited.GetValueOrDefault(d.Id), paid.GetValueOrDefault(d.Id)), d.Currency, paid.GetValueOrDefault(d.Id), plans.TryGetValue(d.Id, out var plan) ? plan : null)).Where(r => r.Balance > 0m)];
    }

    public async Task<string?> NextReceiptNumberAsync(CancellationToken cancellationToken = default)
    {
        var sequence = await db.NumberSequences.AsNoTracking().FirstOrDefaultAsync(s => s.DocumentType == DocumentType.Receipt, cancellationToken);
        return sequence is null ? null : InvoiceBuilder.Domain.ValueObjects.DocumentNumber.Create(sequence.Prefix, sequence.NextValue, sequence.Padding).Value.Value;
    }
}
