using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Payments;
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
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(config);
        _gateway.VerifyCallback(_businessId, Arg.Any<IReadOnlyDictionary<string, string>>()).Returns(false);

        var result = await Controller(new() { ["reference"] = "INV-1", ["hash"] = "BAD" }).Receive(_businessId, CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_unknown_reference_returns_200_and_confirms_nothing()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "123", "encrypted", false, DateTimeOffset.UtcNow).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(config);
        _gateway.VerifyCallback(_businessId, Arg.Any<IReadOnlyDictionary<string, string>>()).Returns(true);
        _payments.GetByReferenceAsync("NOPE", Arg.Any<CancellationToken>()).Returns((Payment?)null);

        var result = await Controller(new() { ["reference"] = "NOPE", ["hash"] = "OK" }).Receive(_businessId, CancellationToken.None);

        result.ShouldBeOfType<OkResult>();
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_unknown_business_returns_400()
    {
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns((PaymentGatewayConfig?)null);

        var result = await Controller(new() { ["reference"] = "INV-1" }).Receive(_businessId, CancellationToken.None);

        result.ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task R19_callback_fields_reach_hash_verification_in_the_order_paynow_sent_them()
    {
        var config = PaymentGatewayConfig.Create(Guid.NewGuid(), _businessId, PaymentProvider.Paynow, "123", "encrypted", false, DateTimeOffset.UtcNow).Value;
        _configs.GetForWebhookAsync(_businessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(config);
        IReadOnlyDictionary<string, string>? seen = null;
        _gateway.VerifyCallback(_businessId, Arg.Do<IReadOnlyDictionary<string, string>>(f => seen = f)).Returns(false);

        await Controller(new()
        {
            ["reference"] = "INV-1",
            ["amount"] = "40.00",
            ["paynowreference"] = "999",
            ["pollurl"] = "https://www.paynow.co.zw/Interface/CheckPayment/?guid=abc",
            ["status"] = "Awaiting Delivery",
            ["hash"] = "X",
        }).Receive(_businessId, CancellationToken.None);

        seen.ShouldNotBeNull();
        seen.Keys.ShouldBe(["reference", "amount", "paynowreference", "pollurl", "status", "hash"]);
        seen["pollurl"].ShouldBe("https://www.paynow.co.zw/Interface/CheckPayment/?guid=abc");
        seen["status"].ShouldBe("Awaiting Delivery");
    }
}
