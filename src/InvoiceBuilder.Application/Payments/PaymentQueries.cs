using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Payments;

public sealed record LedgerRow(Guid Id, DateTimeOffset PaidAt, string ClientName, string AppliedTo, PaymentMethod Method, decimal Amount, string Currency, PaymentStatus Status);

public sealed record OpenInvoiceRow(Guid Id, string Number, DateOnly? DueDate, decimal Balance, string Currency, decimal Paid = 0m, IReadOnlyList<decimal>? InstalmentAmounts = null);

public interface IPaymentQueries
{
    Task<IReadOnlyList<LedgerRow>> LedgerAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenInvoiceRow>> OpenInvoicesAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task<string?> NextReceiptNumberAsync(CancellationToken cancellationToken = default);
}

public sealed record GetPaymentLedgerQuery(DateTimeOffset From, DateTimeOffset Until) : IRequest<Result<IReadOnlyList<LedgerRow>>>;

public sealed record GetOpenInvoicesQuery(Guid ClientId) : IRequest<Result<IReadOnlyList<OpenInvoiceRow>>>;

public sealed record GetNextReceiptNumberQuery : IRequest<Result<string?>>;

public sealed class PaymentQueryHandlers(IPaymentQueries queries)
    : IRequestHandler<GetPaymentLedgerQuery, Result<IReadOnlyList<LedgerRow>>>,
      IRequestHandler<GetOpenInvoicesQuery, Result<IReadOnlyList<OpenInvoiceRow>>>,
      IRequestHandler<GetNextReceiptNumberQuery, Result<string?>>
{
    public async Task<Result<IReadOnlyList<LedgerRow>>> Handle(GetPaymentLedgerQuery request, CancellationToken cancellationToken) =>
        Result.Success(await queries.LedgerAsync(request.From, request.Until, cancellationToken));

    public async Task<Result<IReadOnlyList<OpenInvoiceRow>>> Handle(GetOpenInvoicesQuery request, CancellationToken cancellationToken) =>
        Result.Success(await queries.OpenInvoicesAsync(request.ClientId, cancellationToken));

    public async Task<Result<string?>> Handle(GetNextReceiptNumberQuery request, CancellationToken cancellationToken) =>
        Result.Success(await queries.NextReceiptNumberAsync(cancellationToken));
}

/// <summary>Payments ledger CSV (spec §4.2): invariant numbers, ISO dates in the business time zone; the download adds the UTF-8 BOM.</summary>
public static class PaymentLedgerCsv
{
    public static string Build(IEnumerable<LedgerRow> rows, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(zone);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder("Date,Client,Applied to,Method,Amount,Currency,Status\r\n");
        foreach (var r in rows)
        {
            sb.AppendJoin(',',
                    TimeZoneInfo.ConvertTime(r.PaidAt, zone).ToString("yyyy-MM-dd", inv),
                    InvoiceBuilder.Application.Documents.DocumentListCsv.Escape(r.ClientName),
                    InvoiceBuilder.Application.Documents.DocumentListCsv.Escape(r.AppliedTo),
                    MethodLabel(r.Method),
                    r.Amount.ToString("0.00", inv),
                    r.Currency,
                    r.Status.ToString())
                .Append("\r\n");
        }

        return sb.ToString();
    }

    public static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.OneMoney => "OneMoney",
        PaymentMethod.InnBucks => "InnBucks",
        _ => method.ToString(),
    };
}
