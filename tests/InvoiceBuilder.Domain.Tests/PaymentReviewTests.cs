using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

public sealed class PaymentReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    private static Payment NewPayment() =>
        Payment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, CurrencyCode.Usd, PaymentMethod.EcoCash, "INV-1-ab", Now, Now).Value;

    [Fact]
    public void R19_flag_for_review_records_the_reason_without_changing_status()
    {
        var payment = NewPayment();

        payment.FlagForReview("Amount mismatch", Now);

        payment.ReviewReason.ShouldBe("Amount mismatch");
        payment.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public void R19_set_provider_details_keeps_existing_values_when_null()
    {
        var payment = NewPayment();
        payment.SetProviderDetails("PN-1", "https://poll", Now);

        payment.SetProviderDetails(null, null, Now);

        payment.ProviderReference.ShouldBe("PN-1");
        payment.PollUrl.ShouldBe("https://poll");
    }

    [Fact]
    public void Gateway_config_requires_id_and_key()
    {
        PaymentGatewayConfig.Create(Guid.NewGuid(), Guid.NewGuid(), PaymentProvider.Paynow, " ", "k", false, Now).IsFailure.ShouldBeTrue();
        PaymentGatewayConfig.Create(Guid.NewGuid(), Guid.NewGuid(), PaymentProvider.Paynow, "id", " ", false, Now).IsFailure.ShouldBeTrue();
        PaymentGatewayConfig.Create(Guid.NewGuid(), Guid.NewGuid(), PaymentProvider.Paynow, "id", "k", false, Now).IsSuccess.ShouldBeTrue();
        PaymentGatewayConfig.Create(Guid.NewGuid(), Guid.NewGuid(), PaymentProvider.Paynow, "id", "k", false, Now).Value.Currency.ShouldBe(CurrencyCode.Usd);
        PaymentGatewayConfig.Create(Guid.NewGuid(), Guid.NewGuid(), PaymentProvider.Paynow, "id", "k", false, Now, CurrencyCode.Zwg).Value.Currency.ShouldBe(CurrencyCode.Zwg);
    }
}
