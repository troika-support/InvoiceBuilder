using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public sealed record PublishTemplateCommand(Guid TemplateId) : ITransactionalCommand<Result<int>>;

public sealed class PublishTemplateHandler(
    ICurrentBusiness currentBusiness,
    ICurrentUser user,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<PublishTemplateCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PublishTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<int>(TemplateErrors.NoBusiness);
        }

        var template = await templates.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || template.BusinessId != businessId)
        {
            return Result.Failure<int>(TemplateErrors.NotFound);
        }

        var draft = template.DraftVersion;
        if (draft is null)
        {
            return Result.Failure<int>(new Error("Template.NoDraft", "Save a draft before publishing."));
        }

        var parsed = TemplateDefinitionJson.Parse(draft.Definition);
        if (parsed.IsFailure)
        {
            return Result.Failure<int>(parsed.Error);
        }

        var valid = TemplateDefinitionValidator.Validate(parsed.Value);
        if (valid.IsFailure)
        {
            return Result.Failure<int>(valid.Error);
        }

        var theme = await templates.GetThemeAsync(template.ThemeId, cancellationToken);
        if (ThemeContrast.Check(DocumentTokens.Parse(theme?.Tokens)).Any(r => r.Level == ContrastLevel.Fail))
        {
            return Result.Failure<int>(new Error("Template.ThemeContrast", "The theme text and background colours are too close (below 3:1). Fix the theme before publishing."));
        }

        var publishedBy = Guid.TryParse(user.UserId, out var id) ? id : Guid.Empty;
        var published = template.PublishDraft(publishedBy, time.GetUtcNow());
        if (published.IsFailure)
        {
            return Result.Failure<int>(published.Error);
        }

        await activity.AddAsync(nameof(Domain.Templates.Template), template.Id, "TemplatePublished", $"{template.Name} v{published.Value.Version} published", cancellationToken);
        return Result.Success(published.Value.Version);
    }
}
