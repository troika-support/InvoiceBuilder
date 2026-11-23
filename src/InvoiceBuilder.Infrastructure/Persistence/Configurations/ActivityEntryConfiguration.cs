using InvoiceBuilder.Domain.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("ActivityEntries");
        builder.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ActorUserId).HasMaxLength(450);
        builder.Property(a => a.ActorLabel).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(2000).IsRequired();
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt });
    }
}
