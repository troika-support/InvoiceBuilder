using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Messaging;

/// <summary>Owner pauses or resumes automatic reminders for one invoice (R30).</summary>
public sealed record SetRemindersPausedCommand(Guid InvoiceId, bool Paused) : ITransactionalCommand<Result>;

public sealed class SetRemindersPausedHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<SetRemindersPausedCommand, Result>
{
    public async Task<Result> Handle(SetRemindersPausedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await documents.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.Type != DocumentType.Invoice || invoice.BusinessId != currentBusiness.BusinessId)
        {
            return Result.Failure(new Error("Document.NotFound", "That document no longer exists."));
        }

        invoice.SetRemindersPaused(request.Paused, timeProvider.GetUtcNow());
        await activity.AddAsync(nameof(Document), invoice.Id, request.Paused ? "RemindersPaused" : "RemindersResumed",
            request.Paused ? "Automatic reminders paused." : "Automatic reminders resumed.", cancellationToken);
        return Result.Success();
    }
}
