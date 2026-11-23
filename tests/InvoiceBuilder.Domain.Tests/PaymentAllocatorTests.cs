using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

public sealed class PaymentAllocatorTests
{
    private static Payment NewPayment(decimal amount, CurrencyCode? currency = null, Guid? clientId = null) =>
        Payment.Create(
            TestData.TenantId, TestData.BusinessId, clientId ?? TestData.ClientId, amount,
            currency ?? CurrencyCode.Usd, PaymentMethod.Cash, "ref-1", TestData.Now, TestData.Now).Value;

    private static AllocationRequest Request(decimal balance, decimal amount, CurrencyCode? currency = null, Guid? clientId = null) =>
        new(Guid.NewGuid(), clientId ?? TestData.ClientId, currency ?? CurrencyCode.Usd, balance, amount);

    [Fact]
    public void R16_allocations_within_payment_and_balances_succeed()
    {
        var payment = NewPayment(1000m);

        var result = PaymentAllocator.Allocate(payment, [Request(800m, 800m), Request(500m, 200m)]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Sum(a => a.Amount).ShouldBe(1000m);
        payment.Allocations.Count.ShouldBe(2);
    }

    [Fact]
    public void R16_allocation_sum_above_the_payment_fails()
    {
        var result = PaymentAllocator.Allocate(NewPayment(1000m), [Request(800m, 800m), Request(500m, 300m)]);

        result.Error.Code.ShouldBe("Payment.AllocationExceedsPayment");
    }

    [Fact]
    public void R16_allocation_above_the_invoice_balance_fails()
    {
        var result = PaymentAllocator.Allocate(NewPayment(1000m), [Request(400m, 500m)]);

        result.Error.Code.ShouldBe("Payment.AllocationExceedsBalance");
    }

    [Fact]
    public void R16_allocation_to_another_clients_invoice_fails()
    {
        var result = PaymentAllocator.Allocate(NewPayment(1000m), [Request(500m, 500m, clientId: Guid.NewGuid())]);

        result.Error.Code.ShouldBe("Payment.ClientMismatch");
    }

    [Fact]
    public void R16_allocation_to_an_invoice_in_another_currency_fails()
    {
        var result = PaymentAllocator.Allocate(NewPayment(1000m), [Request(500m, 500m, CurrencyCode.Zwg)]);

        result.Error.Code.ShouldBe("Payment.CurrencyMismatch");
    }

    [Fact]
    public void R17_part_payment_marks_invoice_part_paid() =>
        PaymentAllocator.InvoiceStatusFor(DocumentStatus.Issued, 2400m, 0m, 600m).ShouldBe(DocumentStatus.PartPaid);

    [Fact]
    public void R17_balance_zero_marks_invoice_paid() =>
        PaymentAllocator.InvoiceStatusFor(DocumentStatus.PartPaid, 2400m, 0m, 2400m).ShouldBe(DocumentStatus.Paid);

    [Fact]
    public void R17_full_credit_note_voids_the_invoice() =>
        PaymentAllocator.InvoiceStatusFor(DocumentStatus.Issued, 2400m, 2400m, 0m).ShouldBe(DocumentStatus.Void);

    [Fact]
    public void R17_balance_is_total_minus_credits_minus_confirmed_allocations() =>
        PaymentAllocator.Balance(2400m, 300m, 600m).ShouldBe(1500m);

    [Fact]
    public void R19_confirming_twice_is_idempotent_and_raises_one_event()
    {
        var payment = NewPayment(100m);

        payment.Confirm(TestData.Now).IsSuccess.ShouldBeTrue();
        payment.Confirm(TestData.Now).IsSuccess.ShouldBeTrue();

        payment.Status.ShouldBe(PaymentStatus.Confirmed);
        payment.DomainEvents.OfType<PaymentConfirmed>().ShouldHaveSingleItem();
    }

    [Fact]
    public void R19_a_failed_payment_cannot_be_confirmed()
    {
        var payment = NewPayment(100m);
        payment.Fail(TestData.Now).IsSuccess.ShouldBeTrue();

        payment.Confirm(TestData.Now).IsFailure.ShouldBeTrue();
    }
}
