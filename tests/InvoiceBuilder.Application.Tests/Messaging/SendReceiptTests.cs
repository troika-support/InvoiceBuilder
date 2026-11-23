using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Messaging;

public sealed class SendReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IOutboundMessageRepository _messages = Substitute.For<IOutboundMessageRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Document _receipt;
    private readonly Payment _payment;
    private readonly Client _client;

    public SendReceiptTests()
    {
        _client = Client.Create(_tenant, _business, "Jane", CurrencyCode.Usd, Now).Value;
        var invoiceId = Guid.NewGuid();

        _receipt = Document.CreateDraft(_tenant, _business, DocumentType.Receipt, _client.Id, CurrencyCode.Usd, Now, invoiceId);
        _receipt.AddLine(new LineInput("Payment", null, 1m, 100m), Now).IsSuccess.ShouldBeTrue();
        _receipt.Issue(new IssueRequest(DocumentNumber.Create("RCT-0001").Value, new DateOnly(2026, 10, 23), 0, "{}", "{}", Guid.NewGuid()), Now).IsSuccess.ShouldBeTrue();

        _payment = Payment.Create(_tenant, _business, _client.Id, 100m, CurrencyCode.Usd, PaymentMethod.EcoCash, "INV-1-ab12", Now, Now, null, "https://poll").Value;
        PaymentAllocator.Allocate(_payment, [new AllocationRequest(invoiceId, _client.Id, CurrencyCode.Usd, 100m, 100m)]).IsSuccess.ShouldBeTrue();
        _payment.Confirm(Now).IsSuccess.ShouldBeTrue();

        _payments.GetByIdAsync(_payment.Id, Arg.Any<CancellationToken>()).Returns(_payment);
        _documents.ListReceiptsAsync(invoiceId, Arg.Any<CancellationToken>()).Returns([_receipt]);
        _clients.GetByIdAsync(_client.Id, Arg.Any<CancellationToken>()).Returns(_client);
        _messages.ListForDocumentAsync(_receipt.Id, Arg.Any<CancellationToken>()).Returns([]);
        _sender.Send(Arg.Any<SendDocumentCommand>(), Arg.Any<CancellationToken>()).Returns(InvoiceBuilder.Domain.Common.Result.Success(1));
    }

    private Task<InvoiceBuilder.Domain.Common.Result<int>> Run(bool alsoWhatsApp = false) =>
        new SendReceiptHandler(_payments, _documents, _clients, _messages, _sender)
            .Handle(new SendReceiptCommand(_payment.Id, alsoWhatsApp), CancellationToken.None);

    [Fact]
    public async Task Email_only_by_default_when_client_has_email_and_whatsapp()
    {
        _client.UpdateContact("jane@x.co", null, "+263771000000", null, null, Now);

        await Run();

        await _sender.Received(1).Send(Arg.Is<SendDocumentCommand>(c => c.EmailTo == "jane@x.co" && c.WhatsAppTo == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Also_whatsapp_sends_to_both_channels()
    {
        _client.UpdateContact("jane@x.co", null, "+263771000000", null, null, Now);

        await Run(alsoWhatsApp: true);

        await _sender.Received(1).Send(Arg.Is<SendDocumentCommand>(c => c.EmailTo == "jane@x.co" && c.WhatsAppTo == "+263771000000"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_back_to_whatsapp_when_client_has_no_email()
    {
        _client.UpdateContact(null, "+263772000000", null, null, null, Now);

        await Run();

        await _sender.Received(1).Send(Arg.Is<SendDocumentCommand>(c => c.EmailTo == null && c.WhatsAppTo == "+263772000000"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_nothing_when_client_has_no_contact()
    {
        var result = await Run();

        result.Value.ShouldBe(0);
        await _sender.DidNotReceive().Send(Arg.Any<SendDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Is_idempotent_when_the_receipt_was_already_sent()
    {
        _client.UpdateContact("jane@x.co", null, null, null, null, Now);
        _messages.ListForDocumentAsync(_receipt.Id, Arg.Any<CancellationToken>())
            .Returns([InvoiceBuilder.Domain.Messaging.OutboundMessage.Queue(_tenant, _business, _receipt.Id, InvoiceBuilder.Domain.Messaging.MessageChannel.Email, "jane@x.co", "receipt", Now)]);

        await Run();

        await _sender.DidNotReceive().Send(Arg.Any<SendDocumentCommand>(), Arg.Any<CancellationToken>());
    }
}
