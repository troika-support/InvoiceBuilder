using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class StartOnlinePaymentR20Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();

    private Document IssuedInvoice(bool allowPart, decimal minPercent)
    {
        var tenant = Guid.NewGuid();
        var business = Business.Create(tenant, "Troika", "Troika (Pvt) Ltd", "hello@troika.co.zw", "Harare", Now);
        business.UpdateDefaults(CurrencyCode.Usd, 14, 14, allowPart, minPercent, null, Now).IsSuccess.ShouldBeTrue();
        var invoice = Document.CreateDraft(tenant, business.Id, DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, Now);
        invoice.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid()), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _documents.ListCreditNotesAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(0m);
        _businesses.GetByIdAsync(invoice.BusinessId, Arg.Any<CancellationToken>()).Returns(business);
        return invoice;
    }

    [Theory]
    [InlineData(false, 30, 50, "Payment.PartNotAllowed")]
    [InlineData(true, 30, 29, "Payment.BelowMinimum")]
    [InlineData(true, 30, 150, "Payment.AmountExceedsBalance")]
    public async Task R20_invalid_amounts_are_rejected(bool allowPart, int minPercent, int amount, string code)
    {
        var invoice = IssuedInvoice(allowPart, minPercent);
        var handler = new StartOnlinePaymentHandler(_documents, _payments, Substitute.For<IPaymentGateway>(), _businesses, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now));
        var result = await handler.Handle(new StartOnlinePaymentCommand(invoice.Id, amount, PaymentMethod.Card, null, "a@b.co"), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
    }
}
