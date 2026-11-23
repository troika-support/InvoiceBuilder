using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace InvoiceBuilder.Integration.Tests.Web;

public sealed class ClientLinkControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IDocumentHtmlService _html = Substitute.For<IDocumentHtmlService>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGatewayConfigRepository _gateways = Substitute.For<IPaymentGatewayConfigRepository>();
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();

    private ClientLinkController Controller() => new(Substitute.For<ISender>(), _documents, Substitute.For<IScopeOverride>(), _html, _payments, _gateways, _clients, Substitute.For<IBusinessRepository>(), Substitute.For<ITemplateRepository>())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private Document Invoice(string token)
    {
        var invoice = Document.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, Now);
        invoice.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid(), null, 14, ClientLinkToken.Hash(token)), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), Arg.Any<CancellationToken>()).Returns(invoice);
        _documents.ListCreditNotesAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _documents.ListReceiptsAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(0m);
        return invoice;
    }

    [Fact]
    public async Task R26_receipt_not_belonging_to_the_token_invoice_is_not_found()
    {
        Invoice("tok");
        var result = await Controller().Receipt("tok", Guid.NewGuid(), CancellationToken.None);
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task R26_receipt_with_unknown_token_is_not_found()
    {
        var result = await Controller().Receipt("nope", Guid.NewGuid(), CancellationToken.None);
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task R19_pay_form_prefills_the_client_phone_encoded()
    {
        var invoice = Invoice("tok");
        var client = Client.Create(invoice.TenantId, invoice.BusinessId, "Jane", CurrencyCode.Usd, Now).Value;
        client.UpdateContact(null, "0771<b>234", null, null, null, Now);
        _clients.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(client);
        _gateways.GetForWebhookAsync(invoice.BusinessId, PaymentProvider.Paynow, invoice.Currency, Arg.Any<CancellationToken>()).Returns(PaymentGatewayConfig.Create(invoice.TenantId, invoice.BusinessId, PaymentProvider.Paynow, "1", "key", false, Now).Value);
        _html.RenderAsync(Arg.Any<Document>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success("<html><head></head><body></body></html>"));

        var result = await Controller().View("tok", CancellationToken.None);

        var body = result.ShouldBeOfType<ContentResult>().Content!;
        body.ShouldContain("value=\"0771&lt;b&gt;234\"");
        body.ShouldContain("Sent with Invoice Builder");
        body.ShouldNotContain("0771<b>234");
    }

    [Fact]
    public async Task R19_paid_page_pending_shows_waiting_message_and_back_link()
    {
        Invoice("tok");
        var result = await Controller().Paid("tok", null, CancellationToken.None);
        var body = result.ShouldBeOfType<ContentResult>().Content!;
        body.ShouldContain("Waiting for confirmation from Paynow");
        body.ShouldContain("Back to invoice");
        body.ShouldContain("Sent with Invoice Builder");
    }

    [Fact]
    public async Task R19_paid_page_with_unknown_token_is_not_found()
    {
        var result = await Controller().Paid("nope", null, CancellationToken.None);
        result.ShouldBeOfType<ContentResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}
