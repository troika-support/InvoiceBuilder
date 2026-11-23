using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Activity;
using InvoiceBuilder.Application.Formatting;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests;

public sealed class DomainEventDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task R33_issued_viewed_accepted_and_payment_confirmed_are_logged()
    {
        var log = Substitute.For<IActivityLog>();
        var dispatcher = new DomainEventDispatcher(log, new DisplayFormatter("Africa/Harare"));
        var doc = Guid.NewGuid();
        var business = Guid.NewGuid();
        var payment = Guid.NewGuid();

        await dispatcher.DispatchAsync(
        [
            new DocumentIssued(doc, business, DocumentType.Invoice, "INV-0150", Now),
            new DocumentViewed(doc, business, Now),
            new QuoteAccepted(doc, business, "Tendai", Now),
            new PaymentConfirmed(payment, business, Guid.NewGuid(), 800m, CurrencyCode.Usd, Now),
        ]);

        await log.Received(1).AddAsync("Document", doc, "Issued", "Issued INV-0150", Arg.Any<CancellationToken>());
        await log.Received(1).AddAsync("Document", doc, "Viewed", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await log.Received(1).AddAsync("Document", doc, "Accepted", "Accepted by Tendai", Arg.Any<CancellationToken>());
        await log.Received(1).AddAsync("Payment", payment, "PaymentConfirmed", "Confirmed $800.00", Arg.Any<CancellationToken>());
    }
}
