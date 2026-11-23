using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Payments;

/// <summary>Client taps a pay button on the link page (Step 19). The token resolves the invoice; the browser never names it.</summary>
public sealed record StartClientPaymentCommand(string Token, decimal? Amount, PaymentMethod Method, string? Phone, string? PayerEmail, bool NextInstalment = false)
    : IRequest<Result<StartedPayment>>;

public sealed class StartClientPaymentHandler(
    ISender sender,
    IDocumentRepository documents,
    IScopeOverride scope,
    IPaymentGatewayConfigRepository gateways,
    IPaymentRepository payments,
    IClientRepository clients)
    : IRequestHandler<StartClientPaymentCommand, Result<StartedPayment>>
{
    public async Task<Result<StartedPayment>> Handle(StartClientPaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        if (invoice is null || invoice.Type != DocumentType.Invoice)
        {
            return Result.Failure<StartedPayment>(ClientLinkErrors.Invalid);
        }

        // Paynow issues one integration per currency: no integration for the invoice's currency means no online payment.
        if (await gateways.GetForWebhookAsync(invoice.BusinessId, PaymentProvider.Paynow, invoice.Currency, cancellationToken) is null)
        {
            return Result.Failure<StartedPayment>(new Error("Payment.NotAvailable", "Online payment isn't available for this invoice."));
        }

        var amount = request.Amount;
        if (request.NextInstalment)
        {
            var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(c => c.Total);
            var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
            var states = invoice.GetInstalmentStates(paid, credited, DateOnly.FromDateTime(DateTime.UtcNow));
            amount = NextInstalmentAmount(states);
            if (amount is not > 0m)
            {
                return Result.Failure<StartedPayment>(new Error("Payment.NothingToPay", "This invoice has nothing left to pay."));
            }
        }

        // The link page doesn't ask for an email, so use the client's if we have one. The gateway decides what
        // Paynow gets when it's empty (the merchant's email in test mode and for mobile money; nothing for card).
        var payerEmail = request.PayerEmail;
        if (string.IsNullOrWhiteSpace(payerEmail))
        {
            payerEmail = (await clients.GetByIdAsync(invoice.ClientId, cancellationToken))?.Email;
        }

        return await sender.Send(
            new StartOnlinePaymentCommand(invoice.Id, amount, request.Method, request.Phone, payerEmail?.Trim() ?? string.Empty, request.Token, request.NextInstalment),
            cancellationToken);
    }

    /// <summary>
    /// R40 "Pay next instalment": everything overdue or due today; if nothing is due yet, the next upcoming instalment.
    /// </summary>
    public static decimal NextInstalmentAmount(IReadOnlyList<InstalmentState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var dueNow = states
            .Where(s => s.Remaining > 0m && s.Status is InstalmentStatus.Overdue or InstalmentStatus.Due)
            .Sum(s => s.Remaining);
        return dueNow > 0m ? dueNow : states.FirstOrDefault(s => s.Remaining > 0m)?.Remaining ?? 0m;
    }
}

public sealed record ClientPaymentStatus(string Status, decimal Amount, string Currency, string Method = "");

/// <summary>Reads the status from our own database only (paynow.md section 3 step 7); query-string values are never trusted.</summary>
public sealed record GetClientPaymentStatusQuery(string Token, Guid PaymentId) : IRequest<Result<ClientPaymentStatus>>;

public sealed class GetClientPaymentStatusHandler(IDocumentRepository documents, IPaymentRepository payments, IScopeOverride scope)
    : IRequestHandler<GetClientPaymentStatusQuery, Result<ClientPaymentStatus>>
{
    public async Task<Result<ClientPaymentStatus>> Handle(GetClientPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await ClientLinkLoader.LoadAsync(documents, scope, request.Token, cancellationToken);
        var payment = invoice is null ? null : await payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (invoice is null || payment is null || payment.BusinessId != invoice.BusinessId || payment.Allocations.All(a => a.DocumentId != invoice.Id))
        {
            return Result.Failure<ClientPaymentStatus>(ClientLinkErrors.Invalid);
        }

        return Result.Success(new ClientPaymentStatus(payment.Status.ToString(), payment.Amount, payment.Currency.Code, payment.Method.ToString()));
    }
}
