using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.Recurring;
using InvoiceBuilder.Domain.Templates;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        db.Database.CurrentTransaction is not null ? new JoinedTransactionScope() : new EfTransactionScope(await db.Database.BeginTransactionAsync(cancellationToken));

    public IReadOnlyList<IDomainEvent> TakeDomainEvents()
    {
        var entities = db.ChangeTracker.Entries<Entity>().Select(e => e.Entity).Where(e => e.DomainEvents.Count > 0).ToList();
        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return events;
    }

    private sealed class JoinedTransactionScope : ITransactionScope { public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; public ValueTask DisposeAsync() => ValueTask.CompletedTask; }

    private sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

public sealed class BusinessRepository(AppDbContext db) : IBusinessRepository
{
    public void Add(Business business) => db.Businesses.Add(business);

    public void AddTaxRate(TaxRate taxRate) => db.TaxRates.Add(taxRate);

    public Task<Business?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Businesses.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Business>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Businesses.OrderBy(b => b.TradingName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaxRate>> ListTaxRatesAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        await db.TaxRates.Where(t => t.BusinessId == businessId).OrderBy(t => t.Name).ToListAsync(cancellationToken);
}

public sealed class ClientRepository(AppDbContext db) : IClientRepository
{
    public void Add(Client client) => db.Clients.Add(client);

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Clients.OrderBy(c => c.Name).ToListAsync(cancellationToken);
}

public sealed class CatalogueRepository(AppDbContext db) : ICatalogueRepository
{
    public void Add(CatalogueItem item) => db.CatalogueItems.Add(item);

    public void Remove(CatalogueItem item) => db.CatalogueItems.Remove(item);

    public Task<CatalogueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.CatalogueItems.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CatalogueItem>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogueItems.Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync(cancellationToken);
}

public sealed class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public void Add(Document document) => db.Documents.Add(document);

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents
            .Include(d => d.Lines)
            .Include(d => d.Instalments)
            .Include(d => d.Acceptance)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        // A long-lived (Blazor circuit) context keeps tracked entities, so a status changed elsewhere (e.g. client acceptance) would be stale.
        if (document is not null && db.Entry(document).State == EntityState.Unchanged)
        {
            await db.Entry(document).ReloadAsync(cancellationToken);
        }

        return document;
    }

    public async Task<IReadOnlyList<Document>> ListCreditNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        await db.Documents
            .Include(d => d.Lines)
            .Where(d => d.Type == DocumentType.CreditNote && d.SourceDocumentId == invoiceId && d.Status != DocumentStatus.Draft)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListReceiptsAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        await db.Documents
            .Include(d => d.Lines)
            .Where(d => d.Type == DocumentType.Receipt && d.SourceDocumentId == invoiceId && d.Status != DocumentStatus.Draft)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListBusinessesWithExpiredQuotesAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rows = await db.Documents
            .IgnoreQueryFilters()
            .Where(d => d.Type == DocumentType.Quote && d.ValidUntil < today
                && (d.Status == DocumentStatus.Issued || d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Viewed))
            .Select(d => new { d.TenantId, d.BusinessId })
            .Distinct()
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.TenantId, r.BusinessId)).ToList();
    }

    public async Task<IReadOnlyList<Document>> ListExpiredQuotesAsync(DateOnly today, CancellationToken cancellationToken = default) =>
        await db.Documents
            .Where(d => d.Type == DocumentType.Quote && d.ValidUntil < today
                && (d.Status == DocumentStatus.Issued || d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Viewed))
            .ToListAsync(cancellationToken);

    public Task<Document?> GetByLinkTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.Documents
            .IgnoreQueryFilters()
            .Include(d => d.Lines)
            .Include(d => d.Instalments)
            .Include(d => d.Acceptance)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.LinkTokenHash == tokenHash, cancellationToken);
}

public sealed class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public void Add(Payment payment) => db.Payments.Add(payment);

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Payments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default) =>
        db.Payments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.ProviderReference == providerReference, cancellationToken);

    public Task<Payment?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        db.Payments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Reference == reference, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await db.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListBusinessesWithPendingAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default)
    {
        var rows = await db.Payments
            .IgnoreQueryFilters()
            .Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt < createdBefore)
            .Select(p => new { p.TenantId, p.BusinessId })
            .Distinct()
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.TenantId, r.BusinessId)).ToList();
    }

    public async Task<decimal> ConfirmedAllocatedTotalAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await db.PaymentAllocations
            .Where(a => a.DocumentId == documentId)
            .Join(db.Payments.Where(p => p.Status == PaymentStatus.Confirmed), a => a.PaymentId, p => p.Id, (a, _) => a.Amount)
            .SumAsync(cancellationToken);
    }
}

public sealed class TemplateRepository(AppDbContext db) : ITemplateRepository
{
    public void Add(Template documentTemplate) => db.Templates.Add(documentTemplate);

    public void AddTheme(Theme theme) => db.Themes.Add(theme);

    public Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Templates.Include(t => t.Versions).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DefaultTemplate>> ListDefaultsAsync(CancellationToken cancellationToken = default) =>
        await db.Set<DefaultTemplate>().ToListAsync(cancellationToken);

    public void AddDefault(DefaultTemplate defaultTemplate) => db.Set<DefaultTemplate>().Add(defaultTemplate);

    public async Task<IReadOnlyDictionary<Guid, int>> CountDocumentsByTemplateAsync(CancellationToken cancellationToken = default) =>
        await db.Documents
            .Where(d => d.TemplateVersionId != null)
            .Join(db.Set<TemplateVersion>(), d => d.TemplateVersionId, v => (Guid?)v.Id, (d, v) => v.TemplateId)
            .GroupBy(id => id)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<Theme>> ListThemesAsync(CancellationToken cancellationToken = default) =>
        await db.Themes.OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Template>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Templates.Include(t => t.Versions).OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public Task<Theme?> GetThemeAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Themes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    public Task<TemplateVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        db.Set<TemplateVersion>().FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

    public async Task<IReadOnlyList<(Template Template, TemplateVersion Version)>> ListCurrentVersionsAsync(DocumentType type, CancellationToken cancellationToken = default)
    {
        var templates = await db.Templates.Include(t => t.Versions).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return templates
            .Where(t => !t.IsArchived && t.DocumentTypes.Contains(type))
            .Select(t => (Template: t, Version: t.Versions.FirstOrDefault(v => v.Id == t.CurrentVersionId)))
            .Where(x => x.Version is not null)
            .Select(x => (x.Template, x.Version!))
            .ToList();
    }
}

public sealed class RecurringRepository(AppDbContext db) : IRecurringRepository
{
    public void Add(RecurringSchedule schedule) => db.RecurringSchedules.Add(schedule);

    public Task<RecurringSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RecurringSchedules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurringSchedule>> ListDueAsync(DateOnly today, CancellationToken cancellationToken = default) =>
        await db.RecurringSchedules.Where(r => !r.Paused && r.NextRunDate <= today).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringSchedule>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.RecurringSchedules.OrderBy(r => r.NextRunDate).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListDueBusinessesAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rows = await db.RecurringSchedules
            .IgnoreQueryFilters()
            .Where(r => !r.Paused && r.NextRunDate <= today)
            .Select(r => new { r.TenantId, r.BusinessId })
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => (r.TenantId, r.BusinessId))];
    }
}

public sealed class NumberSequenceRepository(AppDbContext db, TimeProvider timeProvider) : INumberSequenceRepository
{
    public void Add(NumberSequence sequence) => db.NumberSequences.Add(sequence);

    public async Task<IReadOnlyList<NumberSequence>> ListAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        await db.NumberSequences.Where(s => s.BusinessId == businessId).ToListAsync(cancellationToken);

    public async Task<Result<DocumentNumber>> AllocateAsync(Guid businessId, DocumentType type, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("AllocateAsync must run inside a transaction (R6).");
        }

        var typeValue = (int)type;
        var sequence = await db.NumberSequences
            .FromSql($"SELECT * FROM NumberSequences WITH (UPDLOCK, ROWLOCK) WHERE BusinessId = {businessId} AND DocumentType = {typeValue}")
            .SingleOrDefaultAsync(cancellationToken);

        if (sequence is null)
        {
            return Result.Failure<DocumentNumber>(new Error("NumberSequence.NotFound", "No number sequence is set up for this document type."));
        }

        return sequence.Allocate(timeProvider.GetUtcNow());
    }
}
