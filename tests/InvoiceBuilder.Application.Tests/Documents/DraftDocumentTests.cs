using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class DraftDocumentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();

    public DraftDocumentTests()
    {
        _business.BusinessId.Returns(BusinessId);
        _businesses.ListTaxRatesAsync(BusinessId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private Document Draft(DocumentType type = DocumentType.Invoice)
    {
        var document = Document.CreateDraft(TenantId, BusinessId, type, Guid.NewGuid(), CurrencyCode.Usd, Now);
        _documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        return document;
    }

    private static DraftLineInput Line(string description = "Brand identity", decimal qty = 1m, decimal price = 100m) =>
        new(description, null, qty, price, null, null, null);

    // VERIFY: the canvas INV-0142 lines; these three add up to the 2,400.00 shown on canvas 2.
    private static IReadOnlyList<DraftLineInput> CanvasLines() =>
    [
        new("Brand identity", "Logo, palette, type and guidelines", 1m, 1500m, null, null, null),
        new("Website design", "Five-page responsive site", 1m, 600m, null, null, null),
        new("Social templates", "Instagram and Facebook", 2m, 150m, null, null, null),
    ];

    private UpsertDraftLinesHandler LinesHandler() => new(_business, _documents, _businesses, TimeProvider.System);

    [Fact]
    public void Lines_validator_requires_description_positive_quantity_and_non_negative_price()
    {
        var validator = new UpsertDraftLinesValidator();
        var command = new UpsertDraftLinesCommand(Guid.NewGuid(), [Line(""), Line(qty: 0m), Line(price: -1m)]);

        var errors = validator.Validate(command).Errors.Select(e => e.PropertyName).ToList();

        errors.ShouldContain("Lines[0].Description");
        errors.ShouldContain("Lines[1].Quantity");
        errors.ShouldContain("Lines[2].UnitPrice");
    }

    [Fact]
    public void Lines_validator_allows_200_lines_but_not_201()
    {
        var validator = new UpsertDraftLinesValidator();

        validator.Validate(new UpsertDraftLinesCommand(Guid.NewGuid(), Enumerable.Repeat(Line(), 200).ToList())).IsValid.ShouldBeTrue();
        validator.Validate(new UpsertDraftLinesCommand(Guid.NewGuid(), Enumerable.Repeat(Line(), 201).ToList()))
            .Errors.ShouldContain(e => e.PropertyName == "Lines");
    }

    [Fact]
    public async Task R13_canvas_sample_totals_2400()
    {
        var document = Draft();

        var result = await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Total.ShouldBe(2400.00m);
        result.Value.Subtotal.ShouldBe(2400.00m);
        document.Lines.Count.ShouldBe(3);
        document.Lines.Select(l => l.Position).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Upsert_replaces_the_full_list_in_order()
    {
        var document = Draft();
        await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);

        var result = await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, [Line("Second", price: 50m), Line("First", price: 25m)]), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        document.Lines.Select(l => l.Description).ShouldBe(["Second", "First"]);
        document.Total.ShouldBe(75m);
    }

    [Fact]
    public async Task R5_lines_cannot_change_once_issued()
    {
        var document = Draft();
        document.AddLine(new LineInput("Work", null, 1m, 10m), Now);
        document.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, new DateOnly(2026, 11, 2), 14, "{}", "{}", Guid.NewGuid()), Now)
            .IsSuccess.ShouldBeTrue();

        var result = await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);

        result.Error.ShouldBe(DocumentErrors.NotEditable);
        document.Total.ShouldBe(10m);
    }

    [Fact]
    public async Task R34_plan_must_sum_to_the_total()
    {
        var document = Draft();
        await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);

        var result = await new SetInstalmentPlanHandler(_business, _documents, TimeProvider.System).Handle(
            new SetInstalmentPlanCommand(document.Id, [
                new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 800m),
                new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2027, 1, 1), 800m),
            ], InstalmentFrequency.Monthly),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Instalment.SumMismatch");
        document.Instalments.ShouldBeEmpty();
    }

    [Fact]
    public async Task R35_on_acceptance_is_rejected_on_invoices()
    {
        var document = Draft(DocumentType.Invoice);
        await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);

        var result = await new SetInstalmentPlanHandler(_business, _documents, TimeProvider.System).Handle(
            new SetInstalmentPlanCommand(document.Id, [
                new InstalmentDraft(InstalmentDueKind.OnAcceptance, null, 1200m),
                new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2027, 1, 1), 1200m),
            ], InstalmentFrequency.Monthly),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Instalment.DueKindNotAllowed");
    }

    [Fact]
    public async Task R34_valid_plan_is_saved_and_cleared()
    {
        var document = Draft(DocumentType.Quote);
        await LinesHandler().Handle(new UpsertDraftLinesCommand(document.Id, CanvasLines()), CancellationToken.None);
        var plan = InstalmentPlanner.Generate(2400m, 3, InstalmentFrequency.Monthly, new DateOnly(2026, 12, 1), InstalmentDueKind.OnAcceptance).Value;

        var set = await new SetInstalmentPlanHandler(_business, _documents, TimeProvider.System)
            .Handle(new SetInstalmentPlanCommand(document.Id, plan, InstalmentFrequency.Monthly), CancellationToken.None);
        set.IsSuccess.ShouldBeTrue();
        document.Instalments.Select(i => i.Amount).ShouldBe([800m, 800m, 800m]);

        var cleared = await new ClearInstalmentPlanHandler(_business, _documents, TimeProvider.System)
            .Handle(new ClearInstalmentPlanCommand(document.Id), CancellationToken.None);
        cleared.IsSuccess.ShouldBeTrue();
        document.Instalments.ShouldBeEmpty();
    }

    [Fact]
    public async Task Duplicate_copies_lines_into_a_new_draft()
    {
        var source = Draft();
        await LinesHandler().Handle(new UpsertDraftLinesCommand(source.Id, CanvasLines()), CancellationToken.None);
        Document? added = null;
        _documents.Add(Arg.Do<Document>(d => added = d));

        var result = await new DuplicateDocumentHandler(_business, _documents, Substitute.For<IActivityLog>(), TimeProvider.System)
            .Handle(new DuplicateDocumentCommand(source.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.Id.ShouldNotBe(source.Id);
        added.Status.ShouldBe(DocumentStatus.Draft);
        added.Number.ShouldBeNull();
        added.Total.ShouldBe(2400m);
        added.Lines.Count.ShouldBe(3);
    }
}
