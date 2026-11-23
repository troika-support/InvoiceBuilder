using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

/// <summary>Sample data for "Preview with sample data" and Test PDF (prompt 16). Includes a 3-part plan so the schedule shows.</summary>
public static class TemplateSampleData
{
    private const string PlaceholderLogoSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 100' preserveAspectRatio='xMidYMid meet'>" +
        "<rect x='1' y='1' width='198' height='98' rx='6' fill='#F3F4F6' stroke='#9CA3AF' stroke-width='2' stroke-dasharray='6 4'/>" +
        "<circle cx='70' cy='38' r='8' fill='#9CA3AF'/>" +
        "<path d='M30 78 L72 52 L96 68 L128 40 L170 78 Z' fill='#9CA3AF'/>" +
        "<text x='100' y='92' font-family='sans-serif' font-size='11' text-anchor='middle' fill='#4B5563'>Your logo</text></svg>";

    private static readonly string PlaceholderLogo =
        "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PlaceholderLogoSvg));

    public static DocumentRenderModel Model(IReadOnlyDictionary<string, string> themeTokens)
    {
        var lines = new List<RenderLine>
        {
            new("Brand identity design", "Logo, palette and guidelines", 1, 450m, "—", 450m),
            new("Business card design", "Front and back", 1, 60m, "—", 60m),
            new("Signage artwork", "Two sizes, print-ready", 2, 95m, "—", 190m),
        };
        var total = lines.Sum(l => l.Amount);
        var part = Math.Round(total / 3, 2);
        var rows = new List<InstalmentRenderRow>
        {
            new(1, new DateOnly(2026, 11, 6), part, "Upcoming", part),
            new(2, new DateOnly(2026, 12, 6), part, "Upcoming", part),
            new(3, new DateOnly(2027, 1, 6), total - (2 * part), "Upcoming", total - (2 * part)),
        };
        return new DocumentRenderModel(
            new BusinessRenderModel("Troika Design Workshop", "Troika Design Workshop (Pvt) Ltd", "hello@troika.example", "+263 77 000 0000", "12 Samora Machel Ave\nHarare", PlaceholderLogo)
            {
                BankDetails = "Troika Design Workshop\nExample Bank, Harare Main\nAccount 0000 0000 0000",
            },
            new ClientRenderModel("Mutare Roofing & Tiles", "accounts@example.com", "+263 77 000 0001", "4 Main Street\nMutare"),
            new DocumentRenderInfo("Invoice", "INV-0150", new DateOnly(2026, 10, 23), new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 22), "USD", total, 0m, 0m, total, 0m, total, "Payment due within 14 days.", "Goods remain the property of the supplier until paid in full.", "https://example.test/d/sample")
            {
                Status = "Draft",
            },
            lines,
            themeTokens)
        {
            Plan = new PlanRenderInfo(3, 1, part, new DateOnly(2026, 11, 6), 0m) { Rows = rows },
            Acceptance = new AcceptanceRenderInfo("Tendai Moyo", new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.Zero)),
        };
    }
}

public sealed record PreviewTemplateQuery(Guid TemplateId, string DefinitionJson) : IRequest<Result<string>>;

public sealed class PreviewTemplateHandler(ICurrentBusiness currentBusiness, ITemplateRepository templates, ITemplateRenderer renderer)
    : IRequestHandler<PreviewTemplateQuery, Result<string>>
{
    public async Task<Result<string>> Handle(PreviewTemplateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await TemplatePreviewer.RenderAsync(currentBusiness, templates, renderer, request.TemplateId, request.DefinitionJson, cancellationToken);
    }
}

/// <summary>Renders the saved draft (or latest published version) with sample data, as PDF bytes.</summary>
public sealed record TemplateTestPdfQuery(Guid TemplateId) : IRequest<Result<byte[]>>;

public sealed class TemplateTestPdfHandler(ICurrentBusiness currentBusiness, ITemplateRepository templates, ITemplateRenderer renderer, IPdfRenderer pdf)
    : IRequestHandler<TemplateTestPdfQuery, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(TemplateTestPdfQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = currentBusiness.BusinessId is null ? null : await templates.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || template.BusinessId != currentBusiness.BusinessId)
        {
            return Result.Failure<byte[]>(TemplateErrors.NotFound);
        }

        var source = template.DraftVersion ?? template.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        if (source is null)
        {
            return Result.Failure<byte[]>(TemplateErrors.NotFound);
        }

        var html = await TemplatePreviewer.RenderAsync(currentBusiness, templates, renderer, request.TemplateId, source.Definition, cancellationToken);
        return html.IsFailure
            ? Result.Failure<byte[]>(html.Error)
            : Result.Success(await pdf.RenderAsync(html.Value, cancellationToken));
    }
}

internal static class TemplatePreviewer
{
    public static async Task<Result<string>> RenderAsync(
        ICurrentBusiness currentBusiness,
        ITemplateRepository templates,
        ITemplateRenderer renderer,
        Guid templateId,
        string definitionJson,
        CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<string>(TemplateErrors.NoBusiness);
        }

        var template = await templates.GetByIdAsync(templateId, cancellationToken);
        if (template is null || template.BusinessId != businessId)
        {
            return Result.Failure<string>(TemplateErrors.NotFound);
        }

        var parsed = TemplateDefinitionJson.Parse(definitionJson);
        if (parsed.IsFailure)
        {
            return Result.Failure<string>(parsed.Error);
        }

        var valid = TemplateDefinitionValidator.Validate(parsed.Value);
        if (valid.IsFailure)
        {
            return Result.Failure<string>(valid.Error);
        }

        var theme = await templates.GetThemeAsync(template.ThemeId, cancellationToken);
        var model = TemplateSampleData.Model(DocumentTokens.Parse(theme?.Tokens));
        return Result.Success(await renderer.RenderHtmlAsync(parsed.Value, model, cancellationToken));
    }
}
