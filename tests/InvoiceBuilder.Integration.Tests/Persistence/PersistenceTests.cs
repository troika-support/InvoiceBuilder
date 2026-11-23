using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Integration.Tests.Persistence;

public sealed class PersistenceTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task R6_parallel_allocations_are_gap_free_and_unique()
    {
        var tenantId = Guid.CreateVersion7();
        var businessId = await SeedBusinessAsync(tenantId, "Gap Free Ltd");

        await using (var setup = fixture.CreateContext(tenantId))
        {
            setup.NumberSequences.Add(NumberSequence.CreateDefault(tenantId, businessId, DocumentType.Invoice, Now));
            await setup.SaveChangesAsync();
        }

        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => AllocateOneAsync(tenantId, businessId)));

        numbers.Order().ShouldBe(Enumerable.Range(1, 20).Select(i => $"INV-{i:0000}"));
    }

    [Fact]
    public async Task R2_business_a_cannot_read_business_b_clients()
    {
        var tenantId = Guid.CreateVersion7();
        var businessA = await SeedBusinessAsync(tenantId, "Business A");
        var businessB = await SeedBusinessAsync(tenantId, "Business B");

        await using (var setup = fixture.CreateContext(tenantId))
        {
            setup.Clients.Add(Client.Create(tenantId, businessA, "Client of A", CurrencyCode.Usd, Now).Value);
            setup.Clients.Add(Client.Create(tenantId, businessB, "Client of B", CurrencyCode.Usd, Now).Value);
            await setup.SaveChangesAsync();
        }

        await using var asA = fixture.CreateContext(tenantId, businessA);
        var names = await asA.Clients.Select(c => c.Name).ToListAsync();

        names.ShouldBe(["Client of A"]);
    }

    [Fact]
    public async Task R1_other_tenant_sees_nothing()
    {
        var tenantId = Guid.CreateVersion7();
        var businessId = await SeedBusinessAsync(tenantId, "Tenant One Biz");

        await using (var setup = fixture.CreateContext(tenantId))
        {
            setup.Clients.Add(Client.Create(tenantId, businessId, "Private client", CurrencyCode.Usd, Now).Value);
            await setup.SaveChangesAsync();
        }

        await using var otherTenant = fixture.CreateContext(Guid.CreateVersion7());

        (await otherTenant.Clients.AnyAsync()).ShouldBeFalse();
        (await otherTenant.Businesses.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Dev_seed_creates_invoice_0150_with_a_paid_first_instalment()
    {
        var tenantId = DevCurrentTenant.DevTenantId;
        await using (var seedDb = fixture.CreateContext(tenantId))
        {
            var seeder = new DevDataSeeder(seedDb, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevDataSeeder>.Instance);
            await seeder.SeedAsync();
            await seeder.SeedAsync();
        }

        await using var db = fixture.CreateContext(tenantId);
        (await db.Clients.CountAsync()).ShouldBe(5);
        (await db.CatalogueItems.CountAsync()).ShouldBe(6);

        var invoice = await db.Documents.Include(d => d.Instalments).SingleAsync(d => d.Type == DocumentType.Invoice);
        invoice.Number!.Value.ShouldBe("INV-0150");
        invoice.Total.ShouldBe(2400.00m);
        invoice.Status.ShouldBe(DocumentStatus.PartPaid);
        invoice.Instalments.Select(i => i.Amount).ShouldBe([800.00m, 800.00m, 800.00m]);

        var next = await db.NumberSequences.SingleAsync(s => s.DocumentType == DocumentType.Invoice);
        next.NextValue.ShouldBe(151);
    }

    [Fact]
    public async Task R26_link_token_hash_finds_exactly_one_document_across_tenants()
    {
        var tenantId = Guid.CreateVersion7();
        var businessId = await SeedBusinessAsync(tenantId, "Link Ltd");
        var hash = InvoiceBuilder.Application.Documents.ClientLinkToken.Hash(InvoiceBuilder.Application.Documents.ClientLinkToken.Generate());

        await using (var setup = fixture.CreateContext(tenantId, businessId))
        {
            var client = Client.Create(tenantId, businessId, "Link client", CurrencyCode.Usd, Now).Value;
            setup.Clients.Add(client);
            var theme = InvoiceBuilder.Domain.Templates.Theme.Create(tenantId, businessId, "Theme", "{}", Now).Value;
            setup.Add(theme);
            var template = InvoiceBuilder.Domain.Templates.Template.Create(tenantId, businessId, "Template", [DocumentType.Invoice], theme.Id, Now).Value;
            var version = template.PublishVersion("{}", Guid.NewGuid(), Now);
            setup.Add(template);
            var invoice = Document.CreateDraft(tenantId, businessId, DocumentType.Invoice, client.Id, CurrencyCode.Usd, Now);
            invoice.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
            invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-9001").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", version.Id, null, 14, hash), Now).IsSuccess.ShouldBeTrue();
            setup.Documents.Add(invoice);
            await setup.SaveChangesAsync();
        }

        await using var anonymous = fixture.CreateContext(Guid.Empty);
        var found = await new DocumentRepository(anonymous).GetByLinkTokenHashAsync(hash);
        var missing = await new DocumentRepository(anonymous).GetByLinkTokenHashAsync("nope");

        found.ShouldNotBeNull();
        found.Number!.Value.ShouldBe("INV-9001");
        missing.ShouldBeNull();
    }

    private async Task<string> AllocateOneAsync(Guid tenantId, Guid businessId)
    {
        await using var db = fixture.CreateContext(tenantId, businessId);
        var repository = new NumberSequenceRepository(db, TimeProvider.System);
        var unitOfWork = new UnitOfWork(db);

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var number = await repository.AllocateAsync(businessId, DocumentType.Invoice);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();

        return number.Value.Value;
    }

    private async Task<Guid> SeedBusinessAsync(Guid tenantId, string name)
    {
        await using var db = fixture.CreateContext(tenantId);
        if (!await db.Tenants.AnyAsync())
        {
            db.Tenants.Add(Tenant.Create("Test tenant", Now, tenantId));
        }

        var business = Business.Create(tenantId, name, name, "test@example.com", "Harare", Now);
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        return business.Id;
    }
}
