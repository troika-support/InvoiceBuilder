using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Recurring;
using InvoiceBuilder.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("Themes");
        builder.Property(t => t.Tokens).HasColumnType("nvarchar(max)");
        AppDbContext.RequireJson(builder, nameof(Theme.Tokens));
        builder.HasIndex(t => new { t.BusinessId, t.Name }).IsUnique();
    }
}

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("Templates");

        var comparer = new ValueComparer<IReadOnlySet<DocumentType>>(
            (a, b) => DocumentTypeMask.ToMask(a!) == DocumentTypeMask.ToMask(b!),
            s => DocumentTypeMask.ToMask(s),
            s => DocumentTypeMask.FromMask(DocumentTypeMask.ToMask(s)));

        builder.Property(t => t.DocumentTypes)
            .HasConversion(
                new ValueConverter<IReadOnlySet<DocumentType>, int>(
                    s => DocumentTypeMask.ToMask(s),
                    m => DocumentTypeMask.FromMask(m)),
                comparer);

        builder.HasMany(t => t.Versions).WithOne().HasForeignKey(v => v.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<Theme>().WithMany().HasForeignKey(t => t.ThemeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.BusinessId, t.Name }).IsUnique();
    }
}

public sealed class DefaultTemplateConfiguration : IEntityTypeConfiguration<DefaultTemplate>
{
    public void Configure(EntityTypeBuilder<DefaultTemplate> builder)
    {
        builder.ToTable("DefaultTemplates");
        builder.HasOne<Template>().WithMany().HasForeignKey(d => d.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.BusinessId, d.DocumentType }).IsUnique();
    }
}

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("TemplateVersions");
        builder.Property(v => v.Definition).HasColumnType("nvarchar(max)");
        AppDbContext.RequireJson(builder, nameof(TemplateVersion.Definition));
        builder.HasIndex(v => new { v.TemplateId, v.Version }).IsUnique();
    }
}

public sealed class RecurringScheduleConfiguration : IEntityTypeConfiguration<RecurringSchedule>
{
    public void Configure(EntityTypeBuilder<RecurringSchedule> builder)
    {
        builder.ToTable("RecurringSchedules");
        builder.HasOne<Document>().WithMany().HasForeignKey(r => r.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.Paused, r.NextRunDate });
    }
}

internal static class DocumentTypeMask
{
    public static int ToMask(IReadOnlySet<DocumentType> types) => types.Aggregate(0, (mask, t) => mask | (1 << (int)t));

    public static IReadOnlySet<DocumentType> FromMask(int mask) =>
        Enum.GetValues<DocumentType>().Where(t => (mask & (1 << (int)t)) != 0).ToHashSet();
}
