using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Integration.Tests;

public sealed class PollPendingPaymentsJobTests
{
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));

    private PollPendingPaymentsJob Job() => new(_payments, _gateway, Substitute.For<IScopeOverride>(), _sender, _clock, NullLogger<PollPendingPaymentsJob>.Instance);

    private Payment PendingPayment(TimeSpan age)
    {
        var created = _clock.GetUtcNow() - age;
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, CurrencyCode.Usd, PaymentMethod.EcoCash, "INV-1-ab12", created, created, null, "https://poll").Value;
        _payments.ListBusinessesWithPendingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<(Guid TenantId, Guid BusinessId)>>([(payment.TenantId, payment.BusinessId)]));
        _payments.ListPendingAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Payment>>([payment]));
        _sender.Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(ConfirmOutcome.StillPending));
        return payment;
    }

    [Fact]
    public async Task R19_payment_younger_than_one_minute_is_not_polled()
    {
        PendingPayment(TimeSpan.FromSeconds(30));
        await Job().RunAsync(CancellationToken.None);
        await _gateway.DidNotReceive().CheckStatusAsync(Arg.Any<Guid>(), Arg.Any<CurrencyCode>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_pending_payment_is_checked_and_confirmed_through_the_command()
    {
        var payment = PendingPayment(TimeSpan.FromMinutes(5));
        var status = new GatewayStatus(payment.Reference, "P1", payment.Amount, GatewayState.Paid);
        _gateway.CheckStatusAsync(payment.BusinessId, CurrencyCode.Usd, "https://poll", Arg.Any<CancellationToken>()).Returns(Result.Success(status));
        await Job().RunAsync(CancellationToken.None);
        await _sender.Received(1).Send(Arg.Is<ConfirmOnlinePaymentCommand>(c => c.PaymentId == payment.Id && c.Status == status), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_payment_older_than_24_hours_is_failed_without_polling()
    {
        var payment = PendingPayment(TimeSpan.FromHours(25));
        await Job().RunAsync(CancellationToken.None);
        await _gateway.DidNotReceive().CheckStatusAsync(Arg.Any<Guid>(), Arg.Any<CurrencyCode>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<ConfirmOnlinePaymentCommand>(c => c.PaymentId == payment.Id && c.Status.State == GatewayState.Failed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_gateway_failure_leaves_the_payment_pending()
    {
        var payment = PendingPayment(TimeSpan.FromMinutes(5));
        _gateway.CheckStatusAsync(payment.BusinessId, CurrencyCode.Usd, "https://poll", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GatewayStatus>(new Error("Gateway.Down", "down")));
        await Job().RunAsync(CancellationToken.None);
        await _sender.DidNotReceive().Send(Arg.Any<ConfirmOnlinePaymentCommand>(), Arg.Any<CancellationToken>());
    }
}
