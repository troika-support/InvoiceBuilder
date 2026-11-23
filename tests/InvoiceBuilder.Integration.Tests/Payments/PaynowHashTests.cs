using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Infrastructure.Payments.Paynow;

namespace InvoiceBuilder.Integration.Tests.Payments;

public sealed class PaynowHashTests
{
    private const string Key = "11111111-1111-1111-1111-111111111111";

    // Independently computed: SHA-512 (uppercase hex) of the field values in order, then the key.
    private const string KnownHash =
        "37DA2D6BDD6703F953DB141E20EEB642B9959032B339A9DE4808F05C1A3A866B86CB7EA4B27EF3E9404F74C0F7C27221D604D58B8B2C9F14338B935E201C9035";

    private static Dictionary<string, string> Fields(string? hash = KnownHash)
    {
        var fields = new Dictionary<string, string>
        {
            ["reference"] = "INV-0150-ab12",
            ["paynowreference"] = "1234567",
            ["amount"] = "40.00",
            ["status"] = "Paid",
            ["pollurl"] = "https://www.paynow.co.zw/Interface/CheckPayment/?guid=abc",
        };
        if (hash is not null)
        {
            fields["hash"] = hash;
        }

        return fields;
    }

    [Fact]
    public void R19_hash_known_vector_is_computed_and_verifies()
    {
        PaynowHash.Compute(Fields(), Key).ShouldBe(KnownHash);
        PaynowHash.Verify(Fields(), Key).ShouldBeTrue();
    }

    [Fact]
    public void R19_hash_comparison_ignores_case_of_the_supplied_hash()
    {
        PaynowHash.Verify(Fields(KnownHash.ToLowerInvariant()), Key).ShouldBeTrue();
    }

    [Fact]
    public void R19_hash_tampered_field_fails()
    {
        var fields = Fields();
        fields["amount"] = "0.01";

        PaynowHash.Verify(fields, Key).ShouldBeFalse();
    }

    [Fact]
    public void R19_hash_wrong_key_fails()
    {
        PaynowHash.Verify(Fields(), "22222222-2222-2222-2222-222222222222").ShouldBeFalse();
    }

    [Fact]
    public void R19_hash_missing_fails()
    {
        PaynowHash.Verify(Fields(hash: null), Key).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Paid", GatewayState.Paid, false)]
    [InlineData("paid", GatewayState.Paid, false)]
    [InlineData("Awaiting Delivery", GatewayState.Paid, false)]
    [InlineData("Delivered", GatewayState.Paid, false)]
    [InlineData("Created", GatewayState.Pending, false)]
    [InlineData("Sent", GatewayState.Pending, false)]
    [InlineData("Cancelled", GatewayState.Cancelled, false)]
    [InlineData("Failed", GatewayState.Failed, false)]
    [InlineData("Disputed", GatewayState.Failed, true)]
    [InlineData("Refunded", GatewayState.Failed, true)]
    [InlineData("something new", GatewayState.Pending, false)]
    [InlineData(null, GatewayState.Pending, false)]
    public void Paynow_status_maps_per_section_5(string? status, GatewayState state, bool review)
    {
        var mapped = PaynowStatusMapper.Map(status);

        mapped.State.ShouldBe(state);
        mapped.FlagForReview.ShouldBe(review);
    }

    [Fact]
    public void R19_hash_matches_the_worked_example_in_paynows_generating_hash_docs()
    {
        // developers.paynow.co.zw/docs/generating_hash.html
        var fields = new Dictionary<string, string>
        {
            ["id"] = "1201",
            ["reference"] = "TEST REF",
            ["amount"] = "99.99",
            ["additionalinfo"] = "A test ticket transaction",
            ["returnurl"] = "http://www.google.com/search?q=returnurl",
            ["resulturl"] = "http://www.google.com/search?q=resulturl",
            ["status"] = "Message",
        };

        PaynowHash.Compute(fields, "3e9fed89-60e1-4ce5-ab6e-6b1eb2d4f977").ShouldBe(
            "2A033FC38798D913D42ECB786B9B19645ADEDBDE788862032F1BD82CF3B92DEF84F316385D5B40DBB35F1A4FD7D5BFE73835174136463CDD48C9366B0749C689");
    }

    [Fact]
    public void R19_inbound_message_is_url_decoded_then_hashed_in_order()
    {
        // The shape of the "Validating Hash" example: an initiate response with encoded URLs.
        var body = "status=Ok&browserurl=https%3a%2f%2fstaging.paynow.co.zw%2fPayment%2fConfirmPayment%2f9510&pollurl=https%3a%2f%2fstaging.paynow.co.zw%2fInterface%2fCheckPayment%2f%3fguid%3dc7ed41da-0159-46da-b428-69549f770413&paynowreference=9510";
        var fields = PaynowGateway.ParseForm(body);
        fields["pollurl"].ShouldBe("https://staging.paynow.co.zw/Interface/CheckPayment/?guid=c7ed41da-0159-46da-b428-69549f770413");
        fields.Keys.ShouldBe(["status", "browserurl", "pollurl", "paynowreference"]);

        var signed = PaynowGateway.ParseForm(body + "&hash=" + PaynowHash.Compute(fields, Key));
        PaynowHash.Verify(signed, Key).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, false, "client@x.co", "owner@biz.co", "owner@biz.co")]
    [InlineData(false, true, "", "owner@biz.co", "owner@biz.co")]
    [InlineData(true, true, "client@x.co", "owner@biz.co", "client@x.co")]
    [InlineData(true, true, "", "owner@biz.co", "owner@biz.co")]
    [InlineData(true, false, "", "owner@biz.co", null)]
    [InlineData(true, false, "client@x.co", "owner@biz.co", "client@x.co")]
    public void Authemail_follows_paynows_test_mode_and_express_checkout_rules(bool live, bool mobile, string payer, string merchant, string? expected)
    {
        var request = new PaymentRequest(Guid.NewGuid(), Guid.NewGuid(), "INV-1", 10m, "USD", "Invoice", payer, null, merchant);
        PaynowGateway.AuthEmail(request, live, mobile).ShouldBe(expected);
    }
}
