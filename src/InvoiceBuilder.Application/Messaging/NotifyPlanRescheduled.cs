using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Messaging;

/// <summary>Tells the client their instalment dates changed (R39). Email when the client has one, otherwise WhatsApp.</summary>
public sealed record NotifyPlanRescheduledCommand(Guid InvoiceId) : IRequest<Result<int>>;

public sealed class NotifyPlanRescheduledHandler(
    IDocumentRepository documents,
    IClientRepository clients,
    ISender sender) : IRequestHandler<NotifyPlanRescheduledCommand, Result<int>>
{
    public async Task<Result<int>> Handle(NotifyPlanRescheduledCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await documents.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<int>(new Error("Send.DocumentNotFound", "That document could not be found."));
        }

        var client = await clients.GetByIdAsync(invoice.ClientId, cancellationToken);
        var email = string.IsNullOrWhiteSpace(client?.Email) ? null : client.Email;
        var whatsApp = email is null ? (string.IsNullOrWhiteSpace(client?.WhatsApp) ? client?.Phone : client.WhatsApp) : null;
        if (email is null && string.IsNullOrWhiteSpace(whatsApp))
        {
            return Result.Failure<int>(new Error("Send.NoRecipient", "This client has no email or WhatsApp number."));
        }

        return await sender.Send(new SendDocumentCommand(invoice.Id, email, whatsApp, null, null, MessageTemplates.PlanRescheduledEmail), cancellationToken);
    }
}
