using InvoiceBuilder.Application.Dashboard;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace InvoiceBuilder.Integration.Tests.Dashboard;

public sealed class DashboardCacheTests
{
    private static DashboardFigures Figures(string currency, decimal outstanding) => new(
        currency,
        new MoneyCount(outstanding, 1),
        new OverdueFigure(0m, 0, null),
        new MoneyCount(0m, 0),
        new CollectedFigure(0m, 0m, "October", null),
        new MoneyCount(0m, 0),
        [],
        []);

    [Fact]
    public async Task Figures_are_reused_per_business_and_currency_until_invalidated()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryDashboardCache(memory);
        var business = Guid.NewGuid();
        var calls = 0;
        Task<DashboardFigures> Create(CancellationToken _) => Task.FromResult(Figures("USD", ++calls));

        (await cache.GetOrCreateAsync(business, "USD", Create)).Outstanding.Amount.ShouldBe(1m);
        (await cache.GetOrCreateAsync(business, "USD", Create)).Outstanding.Amount.ShouldBe(1m);
        (await cache.GetOrCreateAsync(business, "ZWG", Create)).Outstanding.Amount.ShouldBe(2m);
        (await cache.GetOrCreateAsync(Guid.NewGuid(), "USD", Create)).Outstanding.Amount.ShouldBe(3m);

        cache.Invalidate(business);

        (await cache.GetOrCreateAsync(business, "USD", Create)).Outstanding.Amount.ShouldBe(4m);
    }
}
