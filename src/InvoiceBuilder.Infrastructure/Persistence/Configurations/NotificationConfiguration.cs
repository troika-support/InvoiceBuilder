using InvoiceBuilder.Application.Notifications;
using InvoiceBuilder.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.LinkPath).HasMaxLength(300);
        builder.Ignore(n => n.IsDismissed);
        builder.Ignore(n => n.DomainEvents);
        builder.HasIndex(n => new { n.BusinessId, n.DismissedAt });
    }
}

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public void Add(Notification notification) => db.Set<Notification>().Add(notification);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<Notification>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Notification>().Where(n => n.DismissedAt == null).OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(cancellationToken);
}
