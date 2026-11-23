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
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant,
    ICurrentBusiness currentBusiness) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<UserBusiness> UserBusinesses => Set<UserBusiness>();

    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();

    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();

    public DbSet<Instalment> Instalments => Set<Instalment>();

    public DbSet<QuoteAcceptance> QuoteAcceptances => Set<QuoteAcceptance>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<PaymentGatewayConfig> PaymentGatewayConfigs => Set<PaymentGatewayConfig>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    public DbSet<Theme> Themes => Set<Theme>();

    public DbSet<RecurringSchedule> RecurringSchedules => Set<RecurringSchedule>();

    public DbSet<InvoiceBuilder.Domain.Notifications.Notification> Notifications => Set<InvoiceBuilder.Domain.Notifications.Notification>();

    public DbSet<InvoiceBuilder.Domain.Activity.ActivityEntry> ActivityEntries => Set<InvoiceBuilder.Domain.Activity.ActivityEntry>();

    // Read through properties so EF re-evaluates them per context instance in the query filters.
    private Guid CurrentTenantId => currentTenant.TenantId;

    private Guid? CurrentBusinessId => currentBusiness.BusinessId;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(200);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<CurrencyCode>().HaveConversion<CurrencyCodeConverter>().HaveMaxLength(3);
        configurationBuilder.Properties<HsCode>().HaveConversion<HsCodeConverter>().HaveMaxLength(8);
        configurationBuilder.Properties<DocumentNumber>().HaveConversion<DocumentNumberConverter>().HaveMaxLength(32);
        configurationBuilder.Properties<TaxPercent>().HaveConversion<TaxPercentConverter>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var modelBuilder = builder;
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
        {
            var clrType = entityType.ClrType;
            var entity = modelBuilder.Entity(clrType);
            entity.Property(nameof(Entity.Id)).ValueGeneratedNever();
            entity.Property(nameof(Entity.RowVersion)).IsRowVersion();
            entity.Ignore(nameof(Entity.DomainEvents));

            if (clrType == typeof(Tenant))
            {
                modelBuilder.Entity<Tenant>().HasQueryFilter(t => t.Id == CurrentTenantId);
                continue;
            }

            entity.HasOne(typeof(Tenant)).WithMany().HasForeignKey(nameof(TenantEntity.TenantId)).OnDelete(DeleteBehavior.Restrict);

            if (typeof(BusinessEntity).IsAssignableFrom(clrType))
            {
                entity.HasOne(typeof(Business)).WithMany().HasForeignKey(nameof(BusinessEntity.BusinessId)).OnDelete(DeleteBehavior.Restrict);
                InvokeFilter(nameof(ApplyBusinessFilter), clrType, modelBuilder);
            }
            else
            {
                InvokeFilter(nameof(ApplyTenantFilter), clrType, modelBuilder);
            }
        }
    }

    private void InvokeFilter(string method, Type clrType, ModelBuilder modelBuilder) =>
        typeof(AppDbContext)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(clrType)
            .Invoke(this, [modelBuilder]);

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder)
        where T : TenantEntity =>
        modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    private void ApplyBusinessFilter<T>(ModelBuilder modelBuilder)
        where T : BusinessEntity =>
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            e.TenantId == CurrentTenantId && (CurrentBusinessId == null || e.BusinessId == CurrentBusinessId));

    internal static void RequireJson<T>(EntityTypeBuilder<T> builder, string column)
        where T : class =>
        builder.ToTable(t => t.HasCheckConstraint($"CK_{typeof(T).Name}_{column}_json", $"ISJSON([{column}]) = 1"));
}
