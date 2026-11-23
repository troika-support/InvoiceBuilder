using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Payments.Fake;
using InvoiceBuilder.Infrastructure.Payments.Paynow;

namespace InvoiceBuilder.Integration.Tests.Payments;

public sealed class FakeGatewayTests
{
    private readonly Guid _businessId = Guid.NewGuid();

    [Fact]
    public async Task Fake_initiate_reports_pending()
    {
        var gateway = new FakePaymentGateway(new FakePaymentState());
        var request = new PaymentRequest(_businessId, Guid.NewGuid(), "INV-1", 40m, "USD", "Test", "a@b.co");
        var initiation = await gateway.InitiateWebAsync(request, CancellationToken.None);
        var status = await gateway.CheckStatusAsync(_businessId, CurrencyCode.Usd, initiation.Value.PollUrl, CancellationToken.None);
        Assert.Equal(GatewayState.Pending, status.Value.State);
    }

    [Fact]
    public void Fake_callback_accepts_valid_hash_and_rejects_tampered()
    {
        var gateway = new FakePaymentGateway(new FakePaymentState());
        var fields = new Dictionary<string, string> { ["reference"] = "INV-3", ["amount"] = "40.00", ["status"] = "Paid" };
        fields["hash"] = PaynowHash.Compute(fields, FakePaymentGateway.IntegrationKey);
        Assert.True(gateway.VerifyCallback(_businessId, CurrencyCode.Usd, fields));
        fields["amount"] = "1.00";
        Assert.False(gateway.VerifyCallback(_businessId, CurrencyCode.Usd, fields));
    }
}
