using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Loads a client's issued invoices and credit notes and confirmed payments (query filters scope the business).</summary>
public sealed class StatementQueries(AppDbContext db) : IStatementQueries
{
    public async Task<StatementSource?> LoadAsync(Guid clientId, TimeZoneInfo zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var client = await db.Clients
            .Where(c => c.Id == clientId)
            .Select(c => new StatementClient(c.Id, c.Name, c.Email, c.Address))
            .FirstOrDefaultAsync(cancellationToken);
        if (client is null)
        {
            return null;
        }

        var documents = await db.Documents
            .Where(d => d.ClientId == clientId && d.Number != null && d.Status != DocumentStatus.Draft
                && (d.Type == DocumentType.Invoice || d.Type == DocumentType.CreditNote))
            .Select(d => new { d.Id, d.Type, Number = d.Number!.Value, d.IssueDate, d.IssuedAt, d.Currency, d.Total, d.SourceDocumentId, d.DueDate })
            .ToListAsync(cancellationToken);
        var numbers = documents.ToDictionary(d => d.Id, d => d.Number);

        var entries = new List<StatementEntry>();
        foreach (var d in documents)
        {
            var date = d.IssueDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(d.IssuedAt ?? DateTimeOffset.MinValue, zone).DateTime);
            if (d.Type == DocumentType.Invoice)
            {
                var due = d.DueDate is { } dd ? $"Invoice, due {dd.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}" : "Invoice";
                entries.Add(new StatementEntry(date, StatementEntryKind.Invoice, d.Currency.Code, d.Number, due, d.Total));
            }
            else
            {
                var against = d.SourceDocumentId is { } source && numbers.TryGetValue(source, out var invoice) ? $"Credit note for {invoice}" : "Credit note";
                entries.Add(new StatementEntry(date, StatementEntryKind.CreditNote, d.Currency.Code, d.Number, against, d.Total));
            }
        }

        var payments = await db.Payments
            .Where(p => p.ClientId == clientId && p.Status == PaymentStatus.Confirmed)
            .Select(p => new { p.Id, p.PaidAt, p.Currency, p.Amount, p.Method, p.Reference })
            .ToListAsync(cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToList();
        var appliedTo = paymentIds.Count == 0 ? [] : (await db.PaymentAllocations
                .Where(a => paymentIds.Contains(a.PaymentId))
                .Select(a => new { a.PaymentId, a.DocumentId })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.PaymentId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(a => numbers.GetValueOrDefault(a.DocumentId)).Where(n => n is not null)));

        foreach (var p in payments)
        {
            var method = p.Method switch
            {
                PaymentMethod.BankTransfer => "Bank transfer",
                PaymentMethod.OneMoney => "OneMoney",
                PaymentMethod.InnBucks => "InnBucks",
                _ => p.Method.ToString(),
            };
            var details = appliedTo.TryGetValue(p.Id, out var applied) && applied.Length > 0 ? $"Payment ({method}) for {applied}" : $"Payment ({method})";
            entries.Add(new StatementEntry(
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(p.PaidAt, zone).DateTime),
                StatementEntryKind.Payment,
                p.Currency.Code,
                p.Reference,
                details,
                p.Amount));
        }

        return new StatementSource(client, entries);
    }
}
