using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public sealed record TemplateListItem(Guid Id, string Name, int? PublishedVersion, int? DraftVersion, int UsedBy, IReadOnlyList<DocumentType> DefaultFor, IReadOnlySet<DocumentType> Types, Guid ThemeId, bool IsArchived = false);
public sealed record ListTemplatesQuery(bool IncludeArchived = false) : IRequest<Result<IReadOnlyList<TemplateListItem>>>;

public sealed class ListTemplatesHandler(ITemplateRepository templates)
    : IRequestHandler<ListTemplatesQuery, Result<IReadOnlyList<TemplateListItem>>>
{
    public async Task<Result<IReadOnlyList<TemplateListItem>>> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
    {
        var all = await templates.ListAsync(cancellationToken);
                var usage = await templates.CountDocumentsByTemplateAsync(cancellationToken);
                var defaults = await templates.ListDefaultsAsync(cancellationToken);
        IReadOnlyList<TemplateListItem> items = all
            .Where(t => request.IncludeArchived || !t.IsArchived)
            .Select(t => new TemplateListItem(
                t.Id,
                t.Name,
                t.Versions.FirstOrDefault(v => v.Id == t.CurrentVersionId)?.Version,
                t.DraftVersion?.Version,
                                usage.GetValueOrDefault(t.Id),
                                                defaults.Where(d => d.TemplateId == t.Id).Select(d => d.DocumentType).ToList(),
                                                t.DocumentTypes,
                                                t.ThemeId,
                                                t.IsArchived))
            .ToList();
        return Result.Success(items);
    }
}
