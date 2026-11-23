using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.Recurring;
using InvoiceBuilder.Domain.Templates;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Application.Abstractions;

public interface IBusinessRepository
{
    void Add(Business business);

    void AddTaxRate(TaxRate taxRate);

    Task<Business?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Business>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxRate>> ListTaxRatesAsync(Guid businessId, CancellationToken cancellationToken = default);
}

public interface IClientRepository
{
    void Add(Client client);

    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken = default);
}

public interface ICatalogueRepository
{
    void Add(CatalogueItem item);

    void Remove(CatalogueItem item);

    Task<CatalogueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogueItem>> ListActiveAsync(CancellationToken cancellationToken = default);
}

public interface IDocumentRepository
{
    void Add(Document document);

    /// <summary>Loads the document with its lines, instalments and quote acceptance.</summary>
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListCreditNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListReceiptsAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Reviewed admin query: businesses with open quotes past ValidUntil, for the unscoped expiry job.</summary>
    Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListBusinessesWithExpiredQuotesAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>Open quotes (Issued, Sent or Viewed) whose ValidUntil is before today, in the current scope.</summary>
    Task<IReadOnlyList<Document>> ListExpiredQuotesAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reviewed admin query (R26): finds one issued document by its link token hash, ignoring query filters because the
    /// client has no session. Callers must scope the request to the document's tenant and business afterwards.
    /// </summary>
    Task<Document?> GetByLinkTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}

public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Payment?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default);

    /// <summary>Our own reference ({InvoiceNo}-{shortId}); the callback carries this one.</summary>
    Task<Payment?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Reviewed admin query: businesses with Pending payments created before the cutoff, for the unscoped polling job.</summary>
    Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListBusinessesWithPendingAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default);

    /// <summary>Sum of allocations from Confirmed payments to the document (R17).</summary>
    Task<decimal> ConfirmedAllocatedTotalAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface ITemplateRepository
{
    void Add(Template documentTemplate);

    void AddTheme(Theme theme);

    Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Template>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Theme>> ListThemesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountDocumentsByTemplateAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DefaultTemplate>> ListDefaultsAsync(CancellationToken cancellationToken = default);

    void AddDefault(DefaultTemplate defaultTemplate);

    Task<Theme?> GetThemeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TemplateVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>Current published version of every template that offers the document type.</summary>
    Task<IReadOnlyList<(Template Template, TemplateVersion Version)>> ListCurrentVersionsAsync(DocumentType type, CancellationToken cancellationToken = default);
}

public interface IRecurringRepository
{
    void Add(RecurringSchedule schedule);

    Task<RecurringSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringSchedule>> ListDueAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringSchedule>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Tenant and business of every due schedule, across all businesses (background job only).</summary>
    Task<IReadOnlyList<(Guid TenantId, Guid BusinessId)>> ListDueBusinessesAsync(DateOnly today, CancellationToken cancellationToken = default);
}

public interface INumberSequenceRepository
{
    void Add(NumberSequence sequence);

    /// <summary>All sequences of a business, tracked so prefixes can be changed (R6).</summary>
    Task<IReadOnlyList<NumberSequence>> ListAsync(Guid businessId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the sequence row (UPDLOCK, ROWLOCK) and takes the next number (R6). Must be called inside a transaction
    /// from <see cref="IUnitOfWork.BeginTransactionAsync"/>; the caller saves and commits.
    /// </summary>
    Task<Result<DocumentNumber>> AllocateAsync(Guid businessId, DocumentType type, CancellationToken cancellationToken = default);
}
