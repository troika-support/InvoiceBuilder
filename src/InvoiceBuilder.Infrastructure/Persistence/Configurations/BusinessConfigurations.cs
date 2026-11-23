using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder) => builder.ToTable("Tenants");
}

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses");
        builder.Property(b => b.Address).HasMaxLength(1000);
        builder.HasIndex(b => new { b.TenantId, b.TradingName }).IsUnique();
    }
}

public sealed class UserBusinessConfiguration : IEntityTypeConfiguration<UserBusiness>
{
    public void Configure(EntityTypeBuilder<UserBusiness> builder)
    {
        builder.ToTable("UserBusinesses");
        builder.Property(u => u.UserId).HasMaxLength(450);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Business>().WithMany().HasForeignKey(u => u.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => new { u.UserId, u.BusinessId }).IsUnique();
    }
}

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRates");
        builder.HasIndex(t => new { t.BusinessId, t.Name, t.EffectiveFrom }).IsUnique();
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences");
        builder.Property(n => n.Prefix).HasMaxLength(20);
        builder.HasIndex(n => new { n.BusinessId, n.DocumentType }).IsUnique();
    }
}

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.Property(c => c.Address).HasMaxLength(1000);
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.HasIndex(c => new { c.BusinessId, c.Name }).IsUnique();
    }
}

public sealed class CatalogueItemConfiguration : IEntityTypeConfiguration<CatalogueItem>
{
    public void Configure(EntityTypeBuilder<CatalogueItem> builder)
    {
        builder.ToTable("CatalogueItems");
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(c => c.TaxRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.BusinessId, c.Name }).IsUnique();
    }
}
