using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Templates;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public sealed record SetDefaultTemplateCommand(Guid TemplateId, DocumentType Type) : ITransactionalCommand<Result>;

public sealed class SetDefaultTemplateHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<SetDefaultTemplateCommand, Result>
{
    public async Task<Result> Handle(SetDefaultTemplateCommand request, CancellationToken cancellationToken)
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

        if (template.IsArchived)
        {
            return Result.Failure(new Error("Template.Archived", "Restore the template before making it a default."));
        }

        if (template.CurrentVersionId is null)
        {
            return Result.Failure(new Error("Template.NotPublished", "Publish the template before making it a default."));
        }

        if (!template.DocumentTypes.Contains(request.Type))
        {
            return Result.Failure(new Error("Template.TypeNotSupported", "This template is not used for that document type."));
        }

        var now = time.GetUtcNow();
        var existing = (await templates.ListDefaultsAsync(cancellationToken)).FirstOrDefault(d => d.DocumentType == request.Type);
        if (existing is null)
        {
            templates.AddDefault(DefaultTemplate.Create(tenant.TenantId, businessId, request.Type, template.Id, now));
        }
        else
        {
            existing.Change(template.Id, now);
        }

        await activity.AddAsync(nameof(Template), template.Id, "TemplateDefaultSet", $"{template.Name} set as default for {request.Type}", cancellationToken);
        return Result.Success();
    }
}
