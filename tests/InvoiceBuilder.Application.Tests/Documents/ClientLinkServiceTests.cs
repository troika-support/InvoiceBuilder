using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class ClientLinkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);

    private sealed class FakeProtector : IClientLinkTokenProtector
    {
        public string Protect(string token) => "p:" + token;

        public string? Unprotect(string? protectedToken) =>
            protectedToken is { Length: > 2 } p && p.StartsWith("p:", StringComparison.Ordinal) ? p[2..] : null;
    }

    private static ClientLinkService Service() =>
        new(new FakeProtector(), Microsoft.Extensions.Options.Options.Create(new AppOptions { PublicBaseUrl = "https://ib.example.com/" }));

    private static Document Issued(string token, string? protectedToken)
    {
        var invoice = Document.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Invoice, Guid.NewGuid(), CurrencyCode.Usd, Now);
        invoice.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        invoice.Issue(new IssueRequest(DocumentNumber.Create("INV-0001").Value, new DateOnly(2026, 11, 23), 14, "{}", "{}", Guid.NewGuid(), null, 14, ClientLinkToken.Hash(token), protectedToken), Now).IsSuccess.ShouldBeTrue();
        return invoice;
    }

    [Fact]
    public void R26_sending_again_reuses_the_stored_link_instead_of_breaking_it()
    {
        var document = Issued("tok", "p:tok");
        var hash = document.LinkTokenHash;

        var token = Service().EnsureToken(document, null, Now);

        token.Value.ShouldBe("tok");
        document.LinkTokenHash.ShouldBe(hash);
    }

    [Fact]
    public void R26_a_known_token_on_an_older_document_is_stored_for_next_time()
    {
        var document = Issued("tok", null);

        Service().EnsureToken(document, "tok", Now).Value.ShouldBe("tok");

        document.LinkTokenProtected.ShouldBe("p:tok");
        Service().CurrentToken(document).ShouldBe("tok");
    }

    [Fact]
    public void R26_an_unrecoverable_older_link_is_replaced_once_and_then_reused()
    {
        var document = Issued("lost", null);

        var first = Service().EnsureToken(document, null, Now).Value;
        var second = Service().EnsureToken(document, null, Now).Value;

        first.ShouldNotBe("lost");
        second.ShouldBe(first);
        document.LinkTokenHash.ShouldBe(ClientLinkToken.Hash(first));
    }

    [Fact]
    public void R26_url_uses_the_public_base_url()
    {
        Service().UrlFor(Issued("tok", "p:tok")).ShouldBe("https://ib.example.com/d/tok");
        Service().UrlFor(Issued("tok", null)).ShouldBeNull();
    }
}
