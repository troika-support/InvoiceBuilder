using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Payments;

/// <summary>Credits an issued invoice in full (Amount null) or in part by amount (R4, R13b, R17). A full credit voids the invoice.</summary>
public sealed record IssueCreditNoteCommand(Guid InvoiceId, decimal? Amount, string Reason) : ITransactionalCommand<Result<Guid>>;

public sealed class IssueCreditNoteValidator : AbstractValidator<IssueCreditNoteCommand>
{
    public IssueCreditNoteValidator()
    {
        RuleFor(c => c.InvoiceId).NotEmpty();
        RuleFor(c => c.Amount).GreaterThan(0m).When(c => c.Amount.HasValue);
        RuleFor(c => c.Reason).NotEmpty().WithMessage("Say why this credit note is being issued.");
    }
}

public sealed class IssueCreditNoteHandler(
    IDocumentRepository documents,
    IPaymentRepository payments,
    INumberSequenceRepository numbers,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<IssueCreditNoteCommand, Result<Guid>>
{
    private static readonly TimeSpan HarareOffset = TimeSpan.FromHours(2);

    public async Task<Result<Guid>> Handle(IssueCreditNoteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var invoice = await documents.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.Type != DocumentType.Invoice || invoice.Number is null || invoice.TemplateVersionId is null)
        {
            return Result.Failure<Guid>(CreditNoteRules.InvoiceNotIssued);
        }

        if (request.Amount is > 0m && request.Amount > invoice.Total)
        {
            return Result.Failure<Guid>(CreditNoteRules.ExceedsInvoice);
        }

        var fraction = request.Amount is { } a && invoice.Total > 0m ? a / invoice.Total : 1m;
        var existing = await documents.ListCreditNotesAsync(invoice.Id, cancellationToken);
        var note = invoice.DraftCreditNote(fraction, now);
        var reason = note.SetCreditReason(request.Reason, now);
        if (reason.IsFailure)
        {
            return Result.Failure<Guid>(reason.Error);
        }

        var issueDate = DateOnly.FromDateTime(now.ToOffset(HarareOffset).DateTime);
        var valid = CreditNoteRules.Validate(invoice, existing, note, issueDate);
        if (valid.IsFailure)
        {
            return Result.Failure<Guid>(valid.Error);
        }

        var number = await numbers.AllocateAsync(invoice.BusinessId, DocumentType.CreditNote, cancellationToken);
        if (number.IsFailure)
        {
            return Result.Failure<Guid>(number.Error);
        }

        var issued = note.Issue(new IssueRequest(number.Value, issueDate, 0, invoice.ClientSnapshot, invoice.BusinessSnapshot, invoice.TemplateVersionId.Value), now);
        if (issued.IsFailure)
        {
            return Result.Failure<Guid>(issued.Error);
        }

        var credited = existing.Sum(n => n.Total) + note.Total;
        var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
        var applied = invoice.ApplyPaymentStatus(credited, paid, now);
        if (applied.IsFailure)
        {
            return Result.Failure<Guid>(applied.Error);
        }

        documents.Add(note);
        await activity.AddAsync("Document", invoice.Id, "CreditNoteIssued", $"Credit note {note.Number!.Value} issued against {invoice.Number.Value}.", cancellationToken);
        return Result.Success(note.Id);
    }
}

/// <summary>Owner moves unpaid instalment dates (R36). Totals never change; a reason is required.</summary>
public sealed record RescheduleInstalmentsCommand(Guid InvoiceId, IReadOnlyList<InstalmentDraft> Proposed, string Reason) : ITransactionalCommand<Result>;

public sealed class RescheduleInstalmentsHandler(
    IDocumentRepository documents,
    IPaymentRepository payments,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<RescheduleInstalmentsCommand, Result>
{
    private static readonly TimeSpan HarareOffset = TimeSpan.FromHours(2);

    public async Task<Result> Handle(RescheduleInstalmentsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var invoice = await documents.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.Type != DocumentType.Invoice)
        {
            return Result.Failure(new Error("Payment.InvoiceNotFound", "That invoice could not be found."));
        }

        var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(n => n.Total);
        var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
        var today = DateOnly.FromDateTime(now.ToOffset(HarareOffset).DateTime);
        var result = invoice.RescheduleInstalments(request.Proposed, request.Reason, paid, credited, today, now);
        if (result.IsFailure)
        {
            return result;
        }

        await activity.AddAsync("Document", invoice.Id, "InstalmentsRescheduled", $"Instalments rescheduled: {request.Reason}", cancellationToken);
        return Result.Success();
    }
}
