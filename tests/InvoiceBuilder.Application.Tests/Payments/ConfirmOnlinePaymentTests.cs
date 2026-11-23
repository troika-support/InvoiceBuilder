using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Payments;

public sealed class ConfirmOnlinePaymentTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Business = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly INumberSequenceRepository _numbers = Substitute.For<INumberSequenceRepository>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly InvoiceBuilder.Application.Notifications.INotificationRepository _notifications = Substitute.For<InvoiceBuilder.Application.Notifications.INotificationRepository>();
    private readonly Document _invoice;
    private readonly Payment _payment;

    public ConfirmOnlinePaymentTests()
    {
        _invoice = Document.CreateDraft(Tenant, Business, DocumentType.Invoice, Client, CurrencyCode.Usd, Now);
        _invoice.AddLine(new LineInput("Roofing", null, 1m, 100m), Now).IsSuccess.ShouldBeTrue();
        _invoice.Issue(
            new IssueRequest(DocumentNumber.Create("INV-0150").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid()),
            Now).IsSuccess.ShouldBeTrue();

        _payment = Payment.Create(
            Tenant, Business, Client, 100m, CurrencyCode.Usd, PaymentMethod.EcoCash, "INV-0150-ab12", Now, Now, null, "https://poll").Value;
        PaymentAllocator
            .Allocate(_payment, [new AllocationRequest(_invoice.Id, Client, CurrencyCode.Usd, 100m, 100m)])
            .IsSuccess.ShouldBeTrue();

        _payments.GetByIdAsync(_payment.Id, Arg.Any<CancellationToken>()).Returns(_payment);
        _documents.GetByIdAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(_invoice);
        _documents.ListCreditNotesAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns([]);
        _payments.ConfirmedAllocatedTotalAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(0m);
        _numbers
            .AllocateAsync(Business, DocumentType.Receipt, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(InvoiceBuilder.Domain.Common.Result.Success(DocumentNumber.Create("RCT-0001").Value)));
    }

    private ConfirmOnlinePaymentHandler Handler() =>
        new(_payments, _documents, _numbers, _activity, _notifications, new FixedTime(Now));

            [Fact]
            public async Task Paid_adds_a_payment_confirmed_notification()
            {
                await Handler().Handle(Command(GatewayState.Paid), CancellationToken.None);

                _notifications.Received(1).Add(Arg.Is<InvoiceBuilder.Domain.Notifications.Notification>(n =>
                    n.Kind == InvoiceBuilder.Domain.Notifications.NotificationKind.PaymentConfirmed && n.BusinessId == Business));
            }

            [Fact]
            public async Task Cancelled_adds_a_payment_failed_notification()
            {
                await Handler().Handle(Command(GatewayState.Cancelled), CancellationToken.None);

                _notifications.Received(1).Add(Arg.Is<InvoiceBuilder.Domain.Notifications.Notification>(n =>
                    n.Kind == InvoiceBuilder.Domain.Notifications.NotificationKind.PaymentFailed));
            }

            [Fact]
            public async Task Duplicate_paid_adds_only_one_notification()
            {
                var handler = Handler();
                await handler.Handle(Command(GatewayState.Paid), CancellationToken.None);
                await handler.Handle(Command(GatewayState.Paid), CancellationToken.None);

                _notifications.Received(1).Add(Arg.Any<InvoiceBuilder.Domain.Notifications.Notification>());
            }

    [Fact]
    public async Task Overpayment_is_confirmed_but_flagged_for_review()
    {
        _payments.ConfirmedAllocatedTotalAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(60m);

        var result = await Handler().Handle(Command(GatewayState.Paid), CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.Confirmed);
        _payment.Status.ShouldBe(PaymentStatus.Confirmed);
        _payment.ReviewReason.ShouldNotBeNull();
        _payment.ReviewReason.ShouldContain("overpaid by 60.00");
    }

    [Fact]
    public async Task Payment_for_a_voided_invoice_is_kept_and_flagged_instead_of_rolled_back()
    {
        _invoice.ApplyPaymentStatus(100m, 0m, Now).IsSuccess.ShouldBeTrue();
        _invoice.Status.ShouldBe(DocumentStatus.Void);

        var result = await Handler().Handle(Command(GatewayState.Paid), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _payment.Status.ShouldBe(PaymentStatus.Confirmed);
        _invoice.Status.ShouldBe(DocumentStatus.Void);
        _payment.ReviewReason!.ShouldContain("voided");
    }

    private ConfirmOnlinePaymentCommand Command(GatewayState state, decimal amount = 100m, bool flag = false) =>
        new(_payment.Id, new GatewayStatus("INV-0150-ab12", "PN-1", amount, state), flag);

    [Fact]
    public async Task R19_paid_confirms_payment_issues_receipt_and_marks_invoice_paid()
    {
        var result = await Handler().Handle(Command(GatewayState.Paid), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(ConfirmOutcome.Confirmed);
        _payment.Status.ShouldBe(PaymentStatus.Confirmed);
        _payment.ProviderReference.ShouldBe("PN-1");
        _invoice.Status.ShouldBe(DocumentStatus.Paid);
        _documents.Received(1).Add(Arg.Is<Document>(d =>
            d.Type == DocumentType.Receipt
            && d.Status == DocumentStatus.Issued
            && d.Number!.Value == "RCT-0001"
            && d.SourceDocumentId == _invoice.Id
            && d.Total == 100m));
        await _activity.Received().AddAsync("Payment", _payment.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R17_partial_allocation_marks_invoice_part_paid()
    {
        var partial = Payment.Create(
            Tenant, Business, Client, 40m, CurrencyCode.Usd, PaymentMethod.EcoCash, "INV-0150-cd34", Now, Now, null, "https://poll").Value;
        PaymentAllocator.Allocate(partial, [new AllocationRequest(_invoice.Id, Client, CurrencyCode.Usd, 100m, 40m)]).IsSuccess.ShouldBeTrue();
        _payments.GetByIdAsync(partial.Id, Arg.Any<CancellationToken>()).Returns(partial);

        var result = await Handler().Handle(
            new ConfirmOnlinePaymentCommand(partial.Id, new GatewayStatus("INV-0150-cd34", "PN-2", 40m, GatewayState.Paid), false),
            CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.Confirmed);
        _invoice.Status.ShouldBe(DocumentStatus.PartPaid);
    }

    [Fact]
    public async Task R19_duplicate_paid_creates_one_receipt()
    {
        var handler = Handler();

        var first = await handler.Handle(Command(GatewayState.Paid), CancellationToken.None);
        var second = await handler.Handle(Command(GatewayState.Paid), CancellationToken.None);

        first.Value.ShouldBe(ConfirmOutcome.Confirmed);
        second.Value.ShouldBe(ConfirmOutcome.AlreadyProcessed);
        _documents.Received(1).Add(Arg.Any<Document>());
        await _numbers.Received(1).AllocateAsync(Business, DocumentType.Receipt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_amount_mismatch_is_not_confirmed_and_is_flagged()
    {
        var result = await Handler().Handle(Command(GatewayState.Paid, amount: 90m), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(ConfirmOutcome.FlaggedForReview);
        _payment.Status.ShouldBe(PaymentStatus.Pending);
        _payment.ReviewReason.ShouldNotBeNull();
        _invoice.Status.ShouldBe(DocumentStatus.Issued);
        _documents.DidNotReceive().Add(Arg.Any<Document>());
    }

    [Fact]
    public async Task R19_cancelled_marks_payment_failed()
    {
        var result = await Handler().Handle(Command(GatewayState.Cancelled), CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.Failed);
        _payment.Status.ShouldBe(PaymentStatus.Failed);
        _documents.DidNotReceive().Add(Arg.Any<Document>());
        await _activity.Received().AddAsync("Payment", _payment.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R19_disputed_or_refunded_fails_and_flags_for_review()
    {
        var result = await Handler().Handle(Command(GatewayState.Failed, flag: true), CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.Failed);
        _payment.Status.ShouldBe(PaymentStatus.Failed);
        _payment.ReviewReason.ShouldNotBeNull();
    }

    [Fact]
    public async Task R19_pending_changes_nothing()
    {
        var result = await Handler().Handle(Command(GatewayState.Pending), CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.StillPending);
        _payment.Status.ShouldBe(PaymentStatus.Pending);
        _documents.DidNotReceive().Add(Arg.Any<Document>());
    }

    [Fact]
    public async Task R19_failed_payment_is_not_resurrected_by_a_late_paid()
    {
        await Handler().Handle(Command(GatewayState.Cancelled), CancellationToken.None);

        var result = await Handler().Handle(Command(GatewayState.Paid), CancellationToken.None);

        result.Value.ShouldBe(ConfirmOutcome.AlreadyProcessed);
        _payment.Status.ShouldBe(PaymentStatus.Failed);
        _documents.DidNotReceive().Add(Arg.Any<Document>());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
