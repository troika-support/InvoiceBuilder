using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

public sealed record EditorLine(
    string Description,
    string? Detail,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineDiscount,
    Guid? TaxRateId,
    decimal TaxPercent,
    string? HsCode,
    Guid? CatalogueItemId,
    decimal LineTotal);

public sealed record EditorInstalment(InstalmentDueKind DueKind, DateOnly? DueDate, decimal Amount, string? Note);

public sealed record DocumentEditorModel(
    Guid Id,
    DocumentType Type,
    DocumentStatus Status,
    string? Number,
    Guid ClientId,
    string Currency,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateOnly? ValidUntil,
    DiscountKind DiscountKind,
    decimal DiscountValue,
    string? Notes,
    string? Terms,
    AllowedPaymentMethods PaymentMethods,
    InstalmentFrequency? PlanFrequency,
    DraftTotals Totals,
    IReadOnlyList<EditorLine> Lines,
    IReadOnlyList<EditorInstalment> Instalments,
    DateTimeOffset UpdatedAt,
    bool IsEditable,
    Guid? TemplateVersionId = null,
    bool RemindersPaused = false);

public sealed record GetDocumentForEditQuery(Guid DocumentId) : IRequest<Result<DocumentEditorModel>>;

public sealed class GetDocumentForEditHandler(ICurrentBusiness currentBusiness, IDocumentRepository documents)
    : IRequestHandler<GetDocumentForEditQuery, Result<DocumentEditorModel>>
{
    public async Task<Result<DocumentEditorModel>> Handle(GetDocumentForEditQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DocumentEditorModel>(loaded.Error);
        }

        var d = loaded.Value;
        return Result.Success(new DocumentEditorModel(
            d.Id,
            d.Type,
            d.Status,
            d.Number?.Value,
            d.ClientId,
            d.Currency.Code,
            d.IssueDate,
            d.DueDate,
            d.ValidUntil,
            d.Discount.Kind,
            d.Discount.Value,
            d.Notes,
            d.Terms,
            d.AllowedPaymentMethods,
            d.PlanFrequency,
            DraftTotals.From(d),
            d.Lines.OrderBy(l => l.Position)
                .Select(l => new EditorLine(l.Description, l.Detail, l.Quantity, l.UnitPrice, l.LineDiscount, l.TaxRateId, l.TaxPercentSnapshot, l.HsCodeSnapshot?.Value, l.CatalogueItemId, l.LineTotal))
                .ToList(),
            d.Instalments.OrderBy(i => i.Position)
                .Select(i => new EditorInstalment(i.DueKind, i.DueDate, i.Amount, i.Note))
                .ToList(),
            d.UpdatedAt,
            d.IsEditable,
            d.TemplateVersionId,
            d.RemindersPaused));
    }
}
