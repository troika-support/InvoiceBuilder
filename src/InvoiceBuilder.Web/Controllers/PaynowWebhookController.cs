using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>
/// Paynow result callback (paynow.md section 3 and 7). Anonymous, form-only, rate limited.
/// The body is never trusted: the hash is verified, then the status is polled from Paynow.
/// Each currency has its own Paynow integration and key, so the result URL names the currency
/// (<c>/webhooks/paynow/{businessId}/{currency}</c>). The older URL without a currency is kept for payments started
/// before integrations were split by currency; those were all made with the integration now saved as USD.
/// </summary>
[ApiController]
[Route("webhooks/paynow")]
public sealed partial class PaynowWebhookController(
    IPaymentGatewayConfigRepository configs,
    IScopeOverride scope,
    IPaymentGateway gateway,
    IPaymentRepository payments,
    ISender sender,
    ILogger<PaynowWebhookController> logger) : ControllerBase
{
    public const string RateLimitPolicy = "paynow-webhook";

    [HttpPost("{businessId:guid}")]
    [Consumes("application/x-www-form-urlencoded")]
    [EnableRateLimiting(RateLimitPolicy)]
    public Task<IActionResult> ReceiveLegacy(Guid businessId, CancellationToken cancellationToken) =>
        ReceiveAsync(businessId, CurrencyCode.Usd, cancellationToken);

    [HttpPost("{businessId:guid}/{currency}")]
    [Consumes("application/x-www-form-urlencoded")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Receive(Guid businessId, string currency, CancellationToken cancellationToken)
    {
        var code = CurrencyCode.Create(currency);
        if (code.IsFailure)
        {
            LogRejected(businessId);
            return BadRequest();
        }

        return await ReceiveAsync(businessId, code.Value, cancellationToken);
    }

    private async Task<IActionResult> ReceiveAsync(Guid businessId, CurrencyCode currency, CancellationToken cancellationToken)
    {
        // Read the raw body: the hash covers the values in the order Paynow sent them, which Request.Form doesn't promise.
        string body;
        using (var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var fields = InvoiceBuilder.Infrastructure.Payments.Paynow.PaynowGateway.ParseForm(body);

        var config = await configs.GetForWebhookAsync(businessId, PaymentProvider.Paynow, currency, cancellationToken);
        if (config is null)
        {
            LogRejected(businessId);
            return BadRequest();
        }

        scope.UseScope(config.TenantId, config.BusinessId);

        if (!gateway.VerifyCallback(businessId, currency, fields))
        {
            LogRejected(businessId);
            return BadRequest();
        }

        if (!fields.TryGetValue("reference", out var reference) || string.IsNullOrWhiteSpace(reference))
        {
            return Ok();
        }

        var payment = await payments.GetByReferenceAsync(reference, cancellationToken);
        if (payment is null || string.IsNullOrWhiteSpace(payment.PollUrl))
        {
            LogUnknownReference(businessId);
            return Ok();
        }

        // Poll with the integration whose key verified this callback: it is the one that started the payment.
        var status = await gateway.CheckStatusAsync(businessId, currency, payment.PollUrl, cancellationToken);
        if (status.IsFailure)
        {
            LogStatusFailed(payment.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var result = await sender.Send(new ConfirmOnlinePaymentCommand(payment.Id, status.Value), cancellationToken);
        if (result.IsSuccess && result.Value == ConfirmOutcome.Confirmed)
        {
            var receipt = await sender.Send(new InvoiceBuilder.Application.Messaging.SendReceiptCommand(payment.Id), cancellationToken);
            if (receipt.IsFailure)
            {
                LogReceiptNotSent(payment.Id);
            }
        }

        return result.IsSuccess ? Ok() : StatusCode(StatusCodes.Status500InternalServerError);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow webhook rejected for business {BusinessId}")]
    private partial void LogRejected(Guid businessId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Paynow webhook for unknown reference, business {BusinessId}")]
    private partial void LogUnknownReference(Guid businessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receipt for payment {PaymentId} could not be queued")]
    private partial void LogReceiptNotSent(Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Paynow status check failed for payment {PaymentId}")]
    private partial void LogStatusFailed(Guid paymentId);
}
