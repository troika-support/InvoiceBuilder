using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Shouldly;

namespace InvoiceBuilder.Integration.Tests.Payments;

public sealed class PaynowWebhookTests
{
    private readonly IPaymentGatewayConfigRepository _configs = Substitute.For<IPaymentGatewayConfigRepository>();
    private readonly IScopeOverride _scope = Substitute.For<IScopeOverride>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _businessId = Guid.NewGuid();

    private PaynowWebhookController Controller(Dictionary<string, StringValues> form)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        var body = string.Join("&", form.Select(f => Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(f.Value.ToString())));
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        return new PaynowWebhookController(_configs, _scope, _gateway, _payments, _sender, NullLogger<PaynowWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    [Fact]
    public async Task R19_bad_hash_returns_400_and_confirms_nothing()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "123", "encrypted", false, DateTimeOffset.UtcNow).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>()).Returns(config);
        _gateway.VerifyCallback(_businessId, CurrencyCode.Usd, Arg.Any<IReadOnlyDictionary<string, string>>()).Returns(false);

        var result = await Controller(new() { ["reference"] = "INV-1", ["hash"] = "BAD" }).Receive(_businessId, "usd", CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_unknown_reference_returns_200_and_confirms_nothing()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "123", "encrypted", false, DateTimeOffset.UtcNow).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>()).Returns(config);
        _gateway.VerifyCallback(_businessId, CurrencyCode.Usd, Arg.Any<IReadOnlyDictionary<string, string>>()).Returns(true);
        _payments.GetByReferenceAsync("NOPE", Arg.Any<CancellationToken>()).Returns((Payment?)null);

        var result = await Controller(new() { ["reference"] = "NOPE", ["hash"] = "OK" }).Receive(_businessId, "usd", CancellationToken.None);

        result.ShouldBeOfType<OkResult>();
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_unknown_business_returns_400()
    {
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>()).Returns((PaymentGatewayConfig?)null);

        var result = await Controller(new() { ["reference"] = "INV-1" }).Receive(_businessId, "usd", CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task R19_callback_fields_reach_hash_verification_in_the_order_paynow_sent_them()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "123", "encrypted", false, DateTimeOffset.UtcNow).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>()).Returns(config);
        IReadOnlyDictionary<string, string>? seen = null;
        _gateway.VerifyCallback(_businessId, CurrencyCode.Usd, Arg.Do<IReadOnlyDictionary<string, string>>(f => seen = f)).Returns(false);

        await Controller(new()
        {
            ["reference"] = "INV-1",
            ["amount"] = "40.00",
            ["paynowreference"] = "999",
            ["pollurl"] = "https://www.paynow.co.zw/Interface/CheckPayment/?guid=abc",
            ["status"] = "Awaiting Delivery",
            ["hash"] = "X",
        }).Receive(_businessId, "usd", CancellationToken.None);

        seen.ShouldNotBeNull();
        seen.Keys.ShouldBe(["reference", "amount", "paynowreference", "pollurl", "status", "hash"]);
        seen["pollurl"].ShouldBe("https://www.paynow.co.zw/Interface/CheckPayment/?guid=abc");
        seen["status"].ShouldBe("Awaiting Delivery");
    }

    [Fact]
    public async Task Zig_callback_is_verified_and_polled_with_the_zig_integration()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "456", "encrypted", false, DateTimeOffset.UtcNow, CurrencyCode.Zwg).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Zwg, Arg.Any<CancellationToken>()).Returns(config);
        _gateway.VerifyCallback(_businessId, CurrencyCode.Zwg, Arg.Any<IReadOnlyDictionary<string, string>>()).Returns(true);
        var now = DateTimeOffset.UtcNow;
        var payment = Payment.Create(config.TenantId, _businessId, Guid.NewGuid(), 50m, CurrencyCode.Zwg, PaymentMethod.EcoCash, "INV-9", now, now, null, "https://poll").Value;
        _payments.GetByReferenceAsync("INV-9", Arg.Any<CancellationToken>()).Returns(payment);
        _gateway.CheckStatusAsync(_businessId, CurrencyCode.Zwg, "https://poll", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GatewayStatus>(new Error("x", "x")));

        var result = await Controller(new() { ["reference"] = "INV-9", ["hash"] = "OK" }).Receive(_businessId, "zwg", CancellationToken.None);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        await _gateway.Received(1).CheckStatusAsync(_businessId, CurrencyCode.Zwg, "https://poll", Arg.Any<CancellationToken>());
        await _configs.DidNotReceive().GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_currency_in_the_url_returns_400()
    {
        var result = await Controller(new() { ["reference"] = "INV-1" }).Receive(_businessId, "eur", CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
        await _configs.DidNotReceiveWithAnyArgs().GetForWebhookAsync(default, default, default!, default);
    }

    [Fact]
    public async Task Old_url_without_a_currency_uses_the_usd_integration()
    {
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>()).Returns((PaymentGatewayConfig?)null);

        var result = await Controller(new() { ["reference"] = "INV-1" }).ReceiveLegacy(_businessId, CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
        await _configs.Received(1).GetForWebhookAsync(_businessId, PaymentProvider.Paynow, CurrencyCode.Usd, Arg.Any<CancellationToken>());
    }
}
