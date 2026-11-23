using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class InstalmentPaymentRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 23);
    private static readonly Error GatewayReached = new("Test.GatewayReached", "The amount checks passed.");
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();

    private Document PlanInvoice()
    {
        var tenant = Guid.NewGuid();
        var business = Business.Create(tenant, "Troika", "Troika (Pvt) Ltd", "hello@troika.co.zw", "Harare", Now);
        business.UpdateDefaults(CurrencyCode.Usd, 14, 14, false, 30m, null, Now).IsSuccess.ShouldBeTrue();
        var invoice = Document.CreateDraft(tenant, business.Id, DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, Now);
        invoice.AddLine(new LineInput("Item", null, 1m, 1000m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        invoice.SetPlan(
            [new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(10), 100m), new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(40), 900m)],
            InstalmentFrequency.Monthly,
            Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, Today, 14, "{}", "{}", Guid.NewGuid()), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _documents.ListCreditNotesAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(0m);
        _businesses.GetByIdAsync(invoice.BusinessId, Arg.Any<CancellationToken>()).Returns(business);
        _gateway.InitiateWebAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(Result.Failure<PaymentInitiation>(GatewayReached));
        return invoice;
    }

    private Task<Result<StartedPayment>> Start(Guid invoiceId, decimal amount, bool next) =>
        new StartOnlinePaymentHandler(_documents, _payments, _gateway, _businesses, Substitute.For<IActivityLog>(), new FakeTimeProvider(Now))
            .Handle(new StartOnlinePaymentCommand(invoiceId, amount, PaymentMethod.Card, null, "a@b.co", null, next), CancellationToken.None);

    [Fact]
    public async Task R40_other_amount_on_a_plan_must_reach_the_R20_minimum()
    {
        var invoice = PlanInvoice();

        var result = await Start(invoice.Id, 150m, next: false);

        result.Error.Code.ShouldBe("Payment.BelowMinimum");
    }

    [Fact]
    public async Task R40_pay_next_instalment_is_allowed_below_the_minimum_even_when_part_payments_are_off()
    {
        var invoice = PlanInvoice();

        var result = await Start(invoice.Id, 100m, next: true);

        result.Error.Code.ShouldBe(GatewayReached.Code);
    }

    [Fact]
    public async Task R20_part_payments_are_always_allowed_on_plans()
    {
        var invoice = PlanInvoice();

        var result = await Start(invoice.Id, 300m, next: false);

        result.Error.Code.ShouldBe(GatewayReached.Code);
    }

    [Fact]
    public void R40_next_instalment_is_everything_overdue_or_due_today_else_the_next_upcoming()
    {
        var drafts = new[]
        {
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(-5), 100m),
            new InstalmentDraft(InstalmentDueKind.Date, Today, 200m),
            new InstalmentDraft(InstalmentDueKind.Date, Today.AddDays(30), 300m),
        };

        StartClientPaymentHandler.NextInstalmentAmount(InstalmentStatusCalculator.Calculate(drafts, 50m, 0m, Today)).ShouldBe(250m);
        StartClientPaymentHandler.NextInstalmentAmount(InstalmentStatusCalculator.Calculate(drafts, 300m, 0m, Today)).ShouldBe(300m);
        StartClientPaymentHandler.NextInstalmentAmount(InstalmentStatusCalculator.Calculate(drafts, 600m, 0m, Today)).ShouldBe(0m);
    }
}
