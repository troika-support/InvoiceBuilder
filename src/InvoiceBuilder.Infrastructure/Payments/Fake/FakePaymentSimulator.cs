using System.Globalization;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Infrastructure.Payments.Paynow;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Payments.Fake;

public enum SimulatedResult
{
    Paid,
    Cancelled,
}

/// <summary>
/// Development tool behind /dev/payments. Records the simulated outcome, then posts a correctly hashed form to the
/// real webhook so confirmation runs through the same code path as Paynow.
/// </summary>
public sealed partial class FakePaymentSimulator(
    IPaymentRepository payments,
    IPaymentGatewayConfigRepository configs,
    IBusinessSecretStore secrets,
    IUnitOfWork unitOfWork,
    FakePaymentState state,
    IHttpClientFactory httpClientFactory,
    TimeProvider time,
    ILogger<FakePaymentSimulator> logger)
{
    public async Task<IReadOnlyList<Payment>> ListPendingAsync(CancellationToken ct) =>
        await payments.ListPendingAsync(ct);

    public async Task<string?> SimulateAsync(Guid paymentId, SimulatedResult result, Uri webhookRoot, TimeSpan delay, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(paymentId, ct);
        if (payment is null || string.IsNullOrWhiteSpace(payment.PollUrl))
        {
            return "That payment has no poll URL, so there is nothing to simulate.";
        }

        await EnsureConfigAsync(payment, ct);

        var gatewayState = result == SimulatedResult.Paid ? GatewayState.Paid : GatewayState.Cancelled;
        var providerReference = $"FAKE-{payment.Reference}";
        state.Set(payment.PollUrl, new GatewayStatus(payment.Reference, providerReference, payment.Amount, gatewayState));

        var webhook = new Uri(webhookRoot, $"/webhooks/paynow/{payment.BusinessId}");
        var form = Form(payment, providerReference, result);

        if (delay <= TimeSpan.Zero)
        {
            return await PostAsync(webhook, form, ct);
        }

        // Fire and forget on purpose: the caller's circuit may end before the delay does.
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay, time, CancellationToken.None);
            await PostAsync(webhook, form, CancellationToken.None);
        }, CancellationToken.None);
        return null;
    }

    private static Dictionary<string, string> Form(Payment payment, string providerReference, SimulatedResult result)
    {
        var fields = new Dictionary<string, string>
        {
            ["reference"] = payment.Reference,
            ["paynowreference"] = providerReference,
            ["amount"] = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["status"] = result == SimulatedResult.Paid ? "Paid" : "Cancelled",
            ["pollurl"] = payment.PollUrl ?? string.Empty,
        };
        fields["hash"] = PaynowHash.Compute(fields, FakePaymentGateway.IntegrationKey);
        return fields;
    }

    private async Task EnsureConfigAsync(Payment payment, CancellationToken ct)
    {
        if (await configs.GetAsync(payment.BusinessId, PaymentProvider.Paynow, ct) is not null)
        {
            return;
        }

        var config = PaymentGatewayConfig.Create(
            payment.TenantId,
            payment.BusinessId,
            PaymentProvider.Paynow,
            "FAKE",
            secrets.Protect(FakePaymentGateway.IntegrationKey),
            false,
            time.GetUtcNow());
        if (config.IsSuccess)
        {
            configs.Add(config.Value);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<string?> PostAsync(Uri webhook, Dictionary<string, string> form, CancellationToken ct)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(nameof(FakePaymentSimulator));
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(webhook, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                LogWebhookFailed((int)response.StatusCode);
                return $"The webhook answered HTTP {(int)response.StatusCode}.";
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            LogWebhookException(ex);
            return "Could not reach the webhook.";
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Simulated webhook answered HTTP {StatusCode}")]
    private partial void LogWebhookFailed(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Simulated webhook call failed")]
    private partial void LogWebhookException(Exception exception);
}
