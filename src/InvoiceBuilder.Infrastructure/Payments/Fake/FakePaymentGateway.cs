using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Payments.Paynow;

namespace InvoiceBuilder.Infrastructure.Payments.Fake;

/// <summary>What the fake "Paynow" currently says about each poll URL. Development only.</summary>
public sealed class FakePaymentState
{
    private readonly Dictionary<string, GatewayStatus> _statuses = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public void Set(string pollUrl, GatewayStatus status)
    {
        lock (_lock)
        {
            _statuses[pollUrl] = status;
        }
    }

    public GatewayStatus? Get(string pollUrl)
    {
        lock (_lock)
        {
            return _statuses.GetValueOrDefault(pollUrl);
        }
    }
}

/// <summary>
/// Stand-in for Paynow when <c>Payments:UseFake</c> is true (Development only, paynow.md section 8).
/// Callbacks are hashed with <see cref="IntegrationKey"/> through the same <see cref="PaynowHash"/> as the real gateway.
/// </summary>
public sealed class FakePaymentGateway(FakePaymentState state) : IPaymentGateway
{
    public const string IntegrationKey = "00000000-0000-0000-0000-00000000fake";

    public Task<Result<PaymentInitiation>> InitiateWebAsync(PaymentRequest request, CancellationToken ct) =>
        Task.FromResult(Initiate(request, "Open /dev/payments to simulate the Paynow result."));

    public Task<Result<PaymentInitiation>> InitiateMobileAsync(PaymentRequest request, string phone, MobileMethod method, CancellationToken ct) =>
        Task.FromResult(Initiate(request, "Open /dev/payments to simulate approving the prompt on the phone."));

    public Task<Result<GatewayStatus>> CheckStatusAsync(Guid businessId, CurrencyCode currency, string pollUrl, CancellationToken ct)
    {
        var status = state.Get(pollUrl);
        return Task.FromResult(status is null
            ? Result.Failure<GatewayStatus>(new Error("Fake.NothingSimulated", "Nothing has been simulated for this payment yet."))
            : Result.Success(status));
    }

    public bool VerifyCallback(Guid businessId, CurrencyCode currency, IReadOnlyDictionary<string, string> fields) =>
        PaynowHash.Verify(fields, IntegrationKey);

    private Result<PaymentInitiation> Initiate(PaymentRequest request, string instructions)
    {
        var pollUrl = $"fake://poll/{request.Reference}";
        state.Set(pollUrl, new GatewayStatus(request.Reference, null, request.Amount, GatewayState.Pending));
        return Result.Success(new PaymentInitiation(null, pollUrl, instructions));
    }
}
