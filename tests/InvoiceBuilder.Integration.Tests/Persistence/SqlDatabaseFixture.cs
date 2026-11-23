using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Integration.Tests.Persistence;

/// <summary>
/// A fresh, migrated database per test class on LocalDB (Docker isn't available on this machine, so the owner chose
/// LocalDB over Testcontainers). Override with IB_TEST_SQL, e.g. a SQL Server instance, without the Database part.
/// </summary>
public sealed class SqlDatabaseFixture : IAsyncLifetime
{
    private readonly string _server =
        Environment.GetEnvironmentVariable("IB_TEST_SQL")
        ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _database = $"IB_Test_{Guid.NewGuid():N}";

    public string ConnectionString => $"{_server};Database={_database}";

    public AppDbContext CreateContext(Guid tenantId, Guid? businessId = null) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .AddInterceptors(new AuditTimestampInterceptor(TimeProvider.System))
                .Options,
            new TestTenant(tenantId),
            new TestBusiness(businessId));

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext(Guid.Empty);
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class TestTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
    }

    private sealed class TestBusiness(Guid? businessId) : ICurrentBusiness
    {
        public Guid? BusinessId => businessId;
    }
}
