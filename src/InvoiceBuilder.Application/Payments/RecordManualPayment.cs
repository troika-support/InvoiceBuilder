using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Payments;

public sealed record ManualAllocation(Guid DocumentId, decimal Amount);

public sealed record RecordedManualPayment(Guid PaymentId, Guid ReceiptId, string ReceiptNumber);

/// <summary>Owner records cash, bank transfer or other money received (R15-R18). Confirmed immediately, with a receipt.</summary>
public sealed record RecordManualPaymentCommand(Guid ClientId, decimal Amount, DateOnly PaidOn, PaymentMethod Method, IReadOnlyList<ManualAllocation> Allocations)
    : ITransactionalCommand<Result<RecordedManualPayment>>;

public sealed class RecordManualPaymentValidator : AbstractValidator<RecordManualPaymentCommand>
{
    public RecordManualPaymentValidator()
    {
        RuleFor(c => c.ClientId).NotEmpty();
        RuleFor(c => c.Amount).GreaterThan(0m);
        RuleFor(c => c.Method).Must(m => m is PaymentMethod.Cash or PaymentMethod.BankTransfer or PaymentMethod.Other).WithMessage("Choose Cash, Bank transfer or Other.");
        RuleFor(c => c.Allocations).NotEmpty().WithMessage("Choose at least one invoice.");
    }
}

public sealed class RecordManualPaymentHandler(
    IDocumentRepository documents,
    IPaymentRepository payments,
    INumberSequenceRepository numbers,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<RecordManualPaymentCommand, Result<RecordedManualPayment>>
{
    private static readonly TimeSpan HarareOffset = TimeSpan.FromHours(2);

    public async Task<Result<RecordedManualPayment>> Handle(RecordManualPaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var requests = new List<AllocationRequest>();
        var invoices = new List<(Document Invoice, decimal Amount)>();
        foreach (var item in request.Allocations)
        {
            var invoice = await documents.GetByIdAsync(item.DocumentId, cancellationToken);
            if (invoice is null || invoice.Type != DocumentType.Invoice || invoice.Number is null || invoice.TemplateVersionId is null)
            {
                return Result.Failure<RecordedManualPayment>(new Error("Payment.InvoiceNotFound", "An invoice could not be found."));
            }

            if (invoice.Status is DocumentStatus.Draft or DocumentStatus.Void or DocumentStatus.Paid)
            {
                return Result.Failure<RecordedManualPayment>(new Error("Payment.NothingToPay", $"{invoice.Number.Value} has nothing left to pay."));
            }

            var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(n => n.Total);
            var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
            requests.Add(new AllocationRequest(invoice.Id, invoice.ClientId, invoice.Currency, PaymentAllocator.Balance(invoice.Total, credited, paid), item.Amount));
            invoices.Add((invoice, item.Amount));
        }

        var first = invoices[0].Invoice;
        var paidAt = new DateTimeOffset(request.PaidOn.ToDateTime(TimeOnly.MinValue), HarareOffset);
        var created = Payment.Create(first.TenantId, first.BusinessId, request.ClientId, request.Amount, first.Currency, request.Method, $"MAN-{Guid.NewGuid().ToString("N")[..8]}", paidAt, now);
        if (created.IsFailure)
        {
            return Result.Failure<RecordedManualPayment>(created.Error);
        }

        var payment = created.Value;
        var allocated = PaymentAllocator.Allocate(payment, requests);
        if (allocated.IsFailure)
        {
            return Result.Failure<RecordedManualPayment>(allocated.Error);
        }

        var confirmed = payment.Confirm(now);
        if (confirmed.IsFailure)
        {
            return Result.Failure<RecordedManualPayment>(confirmed.Error);
        }

        foreach (var (invoice, amount) in invoices)
        {
            var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(n => n.Total);
            var paidBefore = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
            var applied = invoice.ApplyPaymentStatus(credited, paidBefore + amount, now);
            if (applied.IsFailure)
            {
                return Result.Failure<RecordedManualPayment>(applied.Error);
            }
        }

        var receipt = await ReceiptIssuing.IssueAsync(numbers, payment, invoices, now, cancellationToken);
        if (receipt.IsFailure)
        {
            return Result.Failure<RecordedManualPayment>(receipt.Error);
        }

        payments.Add(payment);
        documents.Add(receipt.Value);
        await activity.AddAsync("Payment", payment.Id, "PaymentRecorded", $"Manual payment {payment.Reference} recorded; receipt {receipt.Value.Number!.Value} issued.", cancellationToken);
        return Result.Success(new RecordedManualPayment(payment.Id, receipt.Value.Id, receipt.Value.Number.Value.ToString()));
    }
}
