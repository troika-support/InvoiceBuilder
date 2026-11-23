using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Messaging;

/// <summary>
/// Emails (or WhatsApps) the receipt for a confirmed online payment. Sent after the confirm transaction has committed.
/// Idempotent: a receipt that already has an outbound message is not sent again.
/// </summary>
public sealed record SendReceiptCommand(Guid PaymentId, bool AlsoWhatsApp = false) : IRequest<Result<int>>;

public sealed class SendReceiptHandler(
    IPaymentRepository payments,
    IDocumentRepository documents,
    IClientRepository clients,
    IOutboundMessageRepository messages,
    ISender sender) : IRequestHandler<SendReceiptCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SendReceiptCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = await payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null || payment.Status != PaymentStatus.Confirmed || payment.Allocations.Count == 0)
        {
            return Result.Success(0);
        }

        var invoiceId = payment.Allocations[0].DocumentId;
        var receipt = (await documents.ListReceiptsAsync(invoiceId, cancellationToken)).LastOrDefault(r => r.Number is not null);
        if (receipt is null || (await messages.ListForDocumentAsync(receipt.Id, cancellationToken)).Count > 0)
        {
            return Result.Success(0);
        }

        var client = await clients.GetByIdAsync(payment.ClientId, cancellationToken);
        var email = string.IsNullOrWhiteSpace(client?.Email) ? null : client.Email;
        var whatsAppNumber = string.IsNullOrWhiteSpace(client?.WhatsApp) ? client?.Phone : client.WhatsApp;
        var whatsApp = email is null || request.AlsoWhatsApp ? whatsAppNumber : null;
        if (email is null && string.IsNullOrWhiteSpace(whatsApp))
        {
            return Result.Success(0);
        }

        return await sender.Send(new SendDocumentCommand(receipt.Id, email, whatsApp, null, null), cancellationToken);
    }
}
