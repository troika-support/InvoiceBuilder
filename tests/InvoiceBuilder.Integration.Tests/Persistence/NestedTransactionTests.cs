using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBuilder.Integration.Tests.Persistence;

public sealed class NestedTransactionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task R35_nested_transactional_commands_roll_back_together_when_the_outer_one_does_not_commit()
    {
        var tenantId = Guid.CreateVersion7();
        await using var db = fixture.CreateContext(tenantId);
        var business = Business.Create(tenantId, "B", "B Ltd", "b@b.co", "Harare", Now);
        db.Tenants.Add(Tenant.Create("Test tenant", Now, tenantId));
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        await using (var outer = await uow.BeginTransactionAsync())
        {
            db.Clients.Add(Client.Create(tenantId, business.Id, "Rolled back", CurrencyCode.Usd, Now).Value);
            await uow.SaveChangesAsync();
            await using var inner = await uow.BeginTransactionAsync();
            await inner.CommitAsync();
        }

        await using var check = fixture.CreateContext(tenantId, business.Id);
        (await check.Clients.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task R35_nested_transactional_commands_persist_when_the_outer_one_commits()
    {
        var tenantId = Guid.CreateVersion7();
        await using var db = fixture.CreateContext(tenantId);
        var business = Business.Create(tenantId, "B", "B Ltd", "b@b.co", "Harare", Now);
        db.Tenants.Add(Tenant.Create("Test tenant", Now, tenantId));
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        await using (var outer = await uow.BeginTransactionAsync())
        {
            db.Clients.Add(Client.Create(tenantId, business.Id, "Kept", CurrencyCode.Usd, Now).Value);
            await uow.SaveChangesAsync();
            await using (var inner = await uow.BeginTransactionAsync())
            {
                await inner.CommitAsync();
            }

            await outer.CommitAsync();
        }

        await using var check = fixture.CreateContext(tenantId, business.Id);
        (await check.Clients.CountAsync()).ShouldBe(1);
    }
}
