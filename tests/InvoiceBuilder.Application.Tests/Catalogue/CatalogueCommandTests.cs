using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Catalogue;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Catalogue;

public sealed class CatalogueCommandTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();
    private readonly ICatalogueRepository _items = Substitute.For<ICatalogueRepository>();
    private readonly ICatalogueQueries _queries = Substitute.For<ICatalogueQueries>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();

    public CatalogueCommandTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _business.BusinessId.Returns(BusinessId);
    }

    private static CatalogueItemInput Input(string name = "Design hour", string? hs = "49119900") =>
        new(name, "Studio time", "hour", 45m, "USD", null, hs, false);

    private static CatalogueItem Item() =>
        CatalogueItem.Create(TenantId, BusinessId, "Design hour", 45m, CurrencyCode.Usd, DateTimeOffset.UtcNow).Value;

    [Fact]
    public async Task R33_create_saves_all_fields_and_is_audited()
    {
        CatalogueItem? added = null;
        _items.Add(Arg.Do<CatalogueItem>(i => added = i));

        var result = await new CreateCatalogueItemHandler(_tenant, _business, _items, _queries, _activity, TimeProvider.System)
            .Handle(new CreateCatalogueItemCommand(Input()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.Unit.ShouldBe("hour");
        added.Description.ShouldBe("Studio time");
        added.HsCode!.Value.ShouldBe("49119900");
        await _activity.Received(1).AddAsync("CatalogueItem", added.Id, "CatalogueItemCreated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_duplicate_name()
    {
        _queries.NameExistsAsync("Design hour", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new CreateCatalogueItemHandler(_tenant, _business, _items, _queries, _activity, TimeProvider.System)
            .Handle(new CreateCatalogueItemCommand(Input()), CancellationToken.None);

        result.Error.ShouldBe(CatalogueErrors.DuplicateName);
        _items.DidNotReceive().Add(Arg.Any<CatalogueItem>());
    }

    [Fact]
    public void R13a_validator_allows_blank_hs_code_and_rejects_bad_one()
    {
        var validator = new CreateCatalogueItemValidator();

        validator.Validate(new CreateCatalogueItemCommand(Input(hs: null))).IsValid.ShouldBeTrue();
        validator.Validate(new CreateCatalogueItemCommand(Input(hs: "12AB"))).Errors.ShouldContain(e => e.PropertyName == "Input.HsCode");
    }

    [Fact]
    public async Task R33_update_changes_item_and_is_audited()
    {
        var item = Item();
        _items.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await new UpdateCatalogueItemHandler(_items, _queries, _activity, TimeProvider.System)
            .Handle(new UpdateCatalogueItemCommand(item.Id, Input("Senior design hour", hs: null) with { UnitPrice = 60m }), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        item.Name.ShouldBe("Senior design hour");
        item.UnitPrice.ShouldBe(60m);
        item.HsCode.ShouldBeNull();
        await _activity.Received(1).AddAsync("CatalogueItem", item.Id, "CatalogueItemUpdated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R13_used_item_cannot_be_deleted()
    {
        var item = Item();
        _items.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        _queries.IsUsedAsync(item.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new DeleteCatalogueItemHandler(_items, _queries, _activity)
            .Handle(new DeleteCatalogueItemCommand(item.Id), CancellationToken.None);

        result.Error.ShouldBe(CatalogueErrors.InUse);
        _items.DidNotReceive().Remove(Arg.Any<CatalogueItem>());
    }

    [Fact]
    public async Task Unused_item_can_be_deleted()
    {
        var item = Item();
        _items.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await new DeleteCatalogueItemHandler(_items, _queries, _activity)
            .Handle(new DeleteCatalogueItemCommand(item.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _items.Received(1).Remove(item);
    }

    [Fact]
    public async Task R33_archive_and_restore_are_audited()
    {
        var item = Item();
        _items.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        var handler = new ArchiveCatalogueItemHandler(_items, _activity, TimeProvider.System);

        await handler.Handle(new ArchiveCatalogueItemCommand(item.Id), CancellationToken.None);
        item.IsArchived.ShouldBeTrue();
        await handler.Handle(new ArchiveCatalogueItemCommand(item.Id, Archive: false), CancellationToken.None);
        item.IsArchived.ShouldBeFalse();

        await _activity.Received(1).AddAsync("CatalogueItem", item.Id, "CatalogueItemArchived", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _activity.Received(1).AddAsync("CatalogueItem", item.Id, "CatalogueItemRestored", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_asks_for_top_ten_with_trimmed_term()
    {
        _queries.SearchAsync("design", 10, Arg.Any<CancellationToken>()).Returns([]);

        var result = await new SearchCatalogueHandler(_business, _queries).Handle(new SearchCatalogueQuery("  design "), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _queries.Received(1).SearchAsync("design", 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void R13_line_keeps_its_snapshot_when_the_item_changes()
    {
        var item = Item();
        var line = new LineInput(item.Name, null, 1m, item.UnitPrice, CatalogueItemId: item.Id);

        item.Update("Renamed", null, null, 99m, CurrencyCode.Usd, null, null, false, DateTimeOffset.UtcNow);
        item.Archive(DateTimeOffset.UtcNow);

        line.Description.ShouldBe("Design hour");
        line.UnitPrice.ShouldBe(45m);
        line.CatalogueItemId.ShouldBe(item.Id);
    }
}
