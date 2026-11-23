using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    public void Configure(EntityTypeBuilder<ReminderLog> builder)
    {
        builder.ToTable("ReminderLogs");
        builder.Property(r => r.InstalmentPositions).HasMaxLength(200).IsRequired();
        builder.HasIndex(r => new { r.DocumentId, r.SentOn }).IsUnique();
    }
}

public sealed class ReminderRepository(AppDbContext db) : IReminderRepository
{
    public async Task<IReadOnlyList<ReminderBusiness>> ListBusinessesAsync(CancellationToken cancellationToken = default) =>
        await db.Set<BusinessSettings>()
            .IgnoreQueryFilters()
            .Where(s => s.RemindersEnabled)
            .Select(s => new ReminderBusiness(s.TenantId, s.BusinessId, new ReminderRules(s.ReminderDaysBeforeDue, s.ReminderOnDueDate, s.ReminderRepeatEveryDays, s.ReminderMaxAfterDue)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListOpenInvoicesAsync(CancellationToken cancellationToken = default) =>
        await db.Documents
            .Include(d => d.Instalments)
            .Where(d => d.Type == DocumentType.Invoice
                && !d.RemindersPaused
                && (d.Status == DocumentStatus.Issued || d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Viewed || d.Status == DocumentStatus.PartPaid))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<bool> HasLogAsync(Guid documentId, DateOnly sentOn, CancellationToken cancellationToken = default) =>
        db.Set<ReminderLog>().AnyAsync(r => r.DocumentId == documentId && r.SentOn == sentOn, cancellationToken);

    public void Add(ReminderLog log) => db.Set<ReminderLog>().Add(log);
}
