using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable("OutboundMessages");
        builder.Property(m => m.To).HasMaxLength(320).IsRequired();
        builder.Property(m => m.TemplateName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ProviderMessageId).HasMaxLength(200);
        builder.Property(m => m.Error).HasMaxLength(500);
        builder.HasIndex(m => new { m.Channel, m.ProviderMessageId });
        builder.HasIndex(m => m.DocumentId);
    }
}

public sealed class OutboundMessageRepository(AppDbContext db) : IOutboundMessageRepository
{
    public void Add(OutboundMessage message) => db.Set<OutboundMessage>().Add(message);

    public Task<OutboundMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Set<OutboundMessage>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OutboundMessage>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        await db.Set<OutboundMessage>().Where(m => m.DocumentId == documentId).OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);

    public Task<OutboundMessage?> GetByProviderIdForWebhookAsync(MessageChannel channel, string providerMessageId, CancellationToken cancellationToken = default) =>
        db.Set<OutboundMessage>().IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Channel == channel && m.ProviderMessageId == providerMessageId, cancellationToken);
}
