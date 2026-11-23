using InvoiceBuilder.Application.Documents;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class ClientLinkTokenTests
{
    [Fact]
    public void R26_tokens_are_unique_and_url_safe()
    {
        var a = ClientLinkToken.Generate();
        var b = ClientLinkToken.Generate();

        a.ShouldNotBe(b);
        a.ShouldNotContain("+");
        a.ShouldNotContain("/");
        a.ShouldNotContain("=");
    }

    [Fact]
    public void R26_hash_is_stable_sha256_hex_and_differs_from_token()
    {
        var token = ClientLinkToken.Generate();

        ClientLinkToken.Hash(token).ShouldBe(ClientLinkToken.Hash(token));
        ClientLinkToken.Hash(token).Length.ShouldBe(64);
        ClientLinkToken.Hash(token).ShouldNotBe(token);
    }
}
