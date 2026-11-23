using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public static class TemplateErrors
{
    public static readonly Error NotFound = new("Template.NotFound", "That template could not be found.");

    public static readonly Error NoBusiness = new("Template.NoBusiness", "Choose a business first.");
}

public sealed record TemplateForDesign(Guid Id, string Name, int? DraftVersion, int? PublishedVersion, string DefinitionJson, IReadOnlyDictionary<string, string> ThemeTokens);

public sealed record GetTemplateForDesignQuery(Guid TemplateId) : IRequest<Result<TemplateForDesign>>;

public sealed class GetTemplateForDesignHandler(ICurrentBusiness currentBusiness, ITemplateRepository templates)
    : IRequestHandler<GetTemplateForDesignQuery, Result<TemplateForDesign>>
{
    public async Task<Result<TemplateForDesign>> Handle(GetTemplateForDesignQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<TemplateForDesign>(TemplateErrors.NoBusiness);
        }

        var template = await templates.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || template.BusinessId != businessId)
        {
            return Result.Failure<TemplateForDesign>(TemplateErrors.NotFound);
        }

        var draft = template.DraftVersion;
        var published = template.Versions.Where(v => !v.IsDraft).OrderByDescending(v => v.Version).FirstOrDefault();
        var source = draft ?? published;
        var json = source?.Definition is { Length: > 2 } d ? d : TemplateDefinitionJson.Serialize(new TemplateDefinition());
        var theme = await templates.GetThemeAsync(template.ThemeId, cancellationToken);
        return Result.Success(new TemplateForDesign(template.Id, template.Name, draft?.Version, published?.Version, json, DocumentTokens.Parse(theme?.Tokens)));
    }
}

public sealed record SaveTemplateDraftCommand(Guid TemplateId, string DefinitionJson) : ITransactionalCommand<Result<int>>;

public sealed class SaveTemplateDraftHandler(
    ICurrentBusiness currentBusiness,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<SaveTemplateDraftCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SaveTemplateDraftCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<int>(TemplateErrors.NoBusiness);
        }

        var parsed = TemplateDefinitionJson.Parse(request.DefinitionJson);
        if (parsed.IsFailure)
        {
            return Result.Failure<int>(parsed.Error);
        }

        var valid = TemplateDefinitionValidator.Validate(parsed.Value);
        if (valid.IsFailure)
        {
            return Result.Failure<int>(valid.Error);
        }

        var template = await templates.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || template.BusinessId != businessId)
        {
            return Result.Failure<int>(TemplateErrors.NotFound);
        }

        var draft = template.SaveDraft(TemplateDefinitionJson.Serialize(parsed.Value), time.GetUtcNow());
        await activity.AddAsync(nameof(Domain.Templates.Template), template.Id, "TemplateDraftSaved", $"Draft v{draft.Version} of {template.Name} saved", cancellationToken);
        return Result.Success(draft.Version);
    }
}

public static class DocumentTokens
{
    public static Dictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == System.Text.Json.JsonValueKind.String))
                {
                    result[p.Name] = p.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return result;
    }
}
