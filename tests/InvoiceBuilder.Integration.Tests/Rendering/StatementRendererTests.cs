using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Application.Formatting;
using InvoiceBuilder.Infrastructure.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceBuilder.Integration.Tests.Rendering;

public sealed class StatementRendererTests
{
    private static string Render(string clientName, IReadOnlyDictionary<string, string>? theme = null)
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 31);
        var statement = new ClientStatement(
            new StatementBusiness("Troika Design Workshop", "Troika (Pvt) Ltd", "hello@troika.co.zw", "+263 77 000 0000", "Harare", null, "CBZ 01234"),
            new StatementClient(Guid.NewGuid(), clientName, null, "1 Samora Machel Ave"),
            from,
            to,
            to,
            StatementCalculator.Build(
                [
                    new StatementEntry(new DateOnly(2026, 9, 1), StatementEntryKind.Invoice, "USD", "INV-0001", "Invoice", 100m),
                    new StatementEntry(new DateOnly(2026, 10, 3), StatementEntryKind.Invoice, "ZWG", "INV-0002", "Invoice", 2500m),
                ],
                from,
                to),
            theme ?? new Dictionary<string, string>());
        return new StatementHtmlRenderer(new FontEmbedder(FontEmbedder.DefaultDirectory), new DisplayFormatter("Africa/Harare"), NullLoggerFactory.Instance).RenderHtml(statement);
    }

    [Fact]
    public void Statement_is_self_contained_with_a_csp_and_no_script()
    {
        var html = Render("Harare Builders");

        html.ShouldContain("Content-Security-Policy");
        html.ShouldNotContain("<script", Case.Insensitive);
        html.ShouldNotContain("http://");
        html.ShouldContain("USD account");
        html.ShouldContain("ZiG account");
        html.ShouldContain("ZiG 2,500.00");
        html.ShouldContain("How to pay");
    }

    [Fact]
    public void Client_values_are_html_encoded()
    {
        var html = Render("<img src=x onerror=alert(1)>");

        html.ShouldNotContain("<img src=x");
        html.ShouldContain("&lt;img src=x onerror=alert(1)&gt;");
    }

    [Fact]
    public void Theme_colours_must_be_hex_so_css_cannot_be_injected()
    {
        var html = Render("Jane", new Dictionary<string, string> { ["accent"] = "red;}body{display:none" });

        html.ShouldNotContain("display:none");
    }
}
