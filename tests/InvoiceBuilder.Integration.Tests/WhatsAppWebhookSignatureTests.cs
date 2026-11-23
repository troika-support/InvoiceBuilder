using System.Security.Cryptography;
using System.Text;
using InvoiceBuilder.Web.Controllers;
using Shouldly;

namespace InvoiceBuilder.Integration.Tests;

public sealed class WhatsAppWebhookSignatureTests
{
    private const string Secret = "app-secret";

    private static string Sign(byte[] body) => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body)).ToLowerInvariant();

    [Fact]
    public void R28_valid_signature_is_accepted()
    {
        var body = Encoding.UTF8.GetBytes("{\"entry\":[]}");

        WhatsAppWebhookController.IsSignatureValid(body, Sign(body), Secret).ShouldBeTrue();
    }

    [Fact]
    public void R28_tampered_body_is_rejected()
    {
        var body = Encoding.UTF8.GetBytes("{\"entry\":[]}");
        var signature = Sign(body);

        WhatsAppWebhookController.IsSignatureValid(Encoding.UTF8.GetBytes("{\"entry\":[1]}"), signature, Secret).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=zz")]
    [InlineData("md5=abcd")]
    public void R28_missing_or_malformed_signature_is_rejected(string? header)
    {
        WhatsAppWebhookController.IsSignatureValid([1, 2, 3], header, Secret).ShouldBeFalse();
    }

    [Fact]
    public void R28_unset_secret_rejects_everything()
    {
        var body = Encoding.UTF8.GetBytes("{}");

        WhatsAppWebhookController.IsSignatureValid(body, Sign(body), null).ShouldBeFalse();
    }
}
