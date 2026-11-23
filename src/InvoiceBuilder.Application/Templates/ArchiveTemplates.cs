using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Templates;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public static class ArchiveErrors
{
    public static readonly Error TemplateIsDefault = new("Template.IsDefault", "This template is the default for a document type. Choose another default first.");

    public static readonly Error ThemeIsDefault = new("Theme.IsDefault", "This theme is the business default. Choose another default theme first.");
}

public sealed record ArchiveTemplateCommand(Guid TemplateId, bool Archive = true) : ITransactionalCommand<Result>;

public sealed class ArchiveTemplateHandler(
    ICurrentBusiness currentBusiness,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<ArchiveTemplateCommand, Result>
{
    public async Task<Result> Handle(ArchiveTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(TemplateErrors.NoBusiness);
        }

        var template = await templates.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null || template.BusinessId != businessId)
        {
            return Result.Failure(TemplateErrors.NotFound);
        }

        var now = time.GetUtcNow();
        if (request.Archive)
        {
            var defaults = await templates.ListDefaultsAsync(cancellationToken);
            if (defaults.Any(d => d.TemplateId == template.Id))
            {
                return Result.Failure(ArchiveErrors.TemplateIsDefault);
            }

            template.Archive(now);
        }
        else
        {
            template.Restore(now);
        }

        await activity.AddAsync(
            nameof(Template),
            template.Id,
            request.Archive ? "TemplateArchived" : "TemplateRestored",
            $"Template {template.Name} {(request.Archive ? "archived" : "restored")}",
            cancellationToken);
        return Result.Success();
    }
}

public sealed record ArchiveThemeCommand(Guid ThemeId, bool Archive = true) : ITransactionalCommand<Result>;

public sealed class ArchiveThemeHandler(
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<ArchiveThemeCommand, Result>
{
    public async Task<Result> Handle(ArchiveThemeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(TemplateErrors.NoBusiness);
        }

        var theme = await templates.GetThemeAsync(request.ThemeId, cancellationToken);
        if (theme is null || theme.BusinessId != businessId)
        {
            return Result.Failure(TemplateErrors.NotFound);
        }

        var now = time.GetUtcNow();
        if (request.Archive)
        {
            var business = await businesses.GetByIdAsync(businessId, cancellationToken);
            if (business?.DefaultThemeId == theme.Id)
            {
                return Result.Failure(ArchiveErrors.ThemeIsDefault);
            }

            theme.Archive(now);
        }
        else
        {
            theme.Restore(now);
        }

        await activity.AddAsync(
            nameof(Theme),
            theme.Id,
            request.Archive ? "ThemeArchived" : "ThemeRestored",
            $"Theme {theme.Name} {(request.Archive ? "archived" : "restored")}",
            cancellationToken);
        return Result.Success();
    }
}
