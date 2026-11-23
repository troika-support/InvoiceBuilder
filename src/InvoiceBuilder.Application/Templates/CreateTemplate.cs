using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Templates;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

/// <summary>Ready-made template definitions shipped with the app (Step 17).</summary>
public interface IBuiltInTemplates
{
    IReadOnlyList<string> Names { get; }

    /// <summary>Definition JSON for a built-in name, or null for blank / unknown.</summary>
    string? Load(string name);
}

public sealed record CreateTemplateCommand(string Name, string? BuiltIn, Guid? ThemeId = null) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateTemplateHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    ITemplateRepository templates,
    IBuiltInTemplates builtIns,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<CreateTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(TemplateErrors.NoBusiness);
        }

        var business = await businesses.GetByIdAsync(businessId, cancellationToken);
        var themeId = request.ThemeId ?? business?.DefaultThemeId;
        if (themeId is not { } chosenThemeId)
        {
            return Result.Failure<Guid>(new Error("Template.NoTheme", "Create a theme before adding a template."));
        }

        if (request.ThemeId is { } requestedTheme)
        {
            var requested = await templates.GetThemeAsync(requestedTheme, cancellationToken);
            if (requested?.BusinessId != businessId)
            {
                return Result.Failure<Guid>(TemplateErrors.NotFound);
            }

            if (requested.IsArchived)
            {
                return Result.Failure<Guid>(new Error("Theme.Archived", "That theme is archived. Restore it or choose another."));
            }
        }

        var definition = request.BuiltIn is { Length: > 0 } name ? builtIns.Load(name) : null;
        if (request.BuiltIn is { Length: > 0 } && definition is null)
        {
            return Result.Failure<Guid>(new Error("Template.UnknownBuiltIn", "That starting template does not exist."));
        }

        definition ??= TemplateDefinitionJson.Serialize(new TemplateDefinition());
        var parsed = TemplateDefinitionJson.Parse(definition);
        if (parsed.IsFailure)
        {
            return Result.Failure<Guid>(parsed.Error);
        }

        var valid = TemplateDefinitionValidator.Validate(parsed.Value);
        if (valid.IsFailure)
        {
            return Result.Failure<Guid>(valid.Error);
        }

        var now = time.GetUtcNow();
        var created = Template.Create(tenant.TenantId, businessId, request.Name, Enum.GetValues<DocumentType>(), chosenThemeId, now);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var template = created.Value;
        template.SaveDraft(TemplateDefinitionJson.Serialize(parsed.Value), now);
        templates.Add(template);
        await activity.AddAsync(nameof(Template), template.Id, "TemplateCreated", $"Template {template.Name} created", cancellationToken);
        return Result.Success(template.Id);
    }
}
