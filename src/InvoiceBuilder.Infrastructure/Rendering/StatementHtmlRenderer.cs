using System.Globalization;
using System.Net;
using System.Text;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Clients;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>
/// The built-in client statement template (spec §4.2): A4, the business's theme colours and fonts, one block per
/// currency (R12). Self-contained like document HTML: fonts and logo inline, no scripts, a CSP that allows only data:.
/// Every value is HTML-encoded; colours come from the theme resolver, which only returns validated hex values.
/// </summary>
public sealed class StatementHtmlRenderer(FontEmbedder fonts, IDisplayFormatter format, ILoggerFactory loggerFactory) : IStatementRenderer
{
    private const string ContentSecurityPolicy = "default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string RenderHtml(ClientStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var theme = new ThemeTokenResolver(statement.ThemeTokens, loggerFactory.CreateLogger<ThemeTokenResolver>());
        var ink = theme.Color("ink");
        var paper = theme.Color("paper", "paper");
        var accent = theme.Color("accent");
        var muted = theme.Color("muted");
        var body = theme.FontFamily("body");
        var mono = theme.FontFamily("mono");
        var b = statement.Business;
        var c = statement.Client;

        var sb = new StringBuilder(16 * 1024);
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"").Append(ContentSecurityPolicy).Append("\">");
        sb.Append("<title>Statement · ").Append(Enc(c.Name)).Append("</title><style>").Append(fonts.FontFaceCss);
        sb.Append(CultureInfo.InvariantCulture, $$"""
            @page{size:210mm 297mm;margin:16mm 14mm 18mm}
            *{box-sizing:border-box}
            body{margin:0;font-family:{{body}},sans-serif;font-size:9.5pt;line-height:1.45;color:{{ink}};background:{{paper}}}
            .mono{font-family:{{mono}},monospace;font-variant-numeric:tabular-nums}
            header{display:flex;justify-content:space-between;gap:12mm;padding-bottom:5mm;border-bottom:1.2mm solid {{accent}}}
            .biz img{max-height:16mm;max-width:50mm;display:block;margin-bottom:2mm}
            .biz strong{font-size:12pt;display:block}
            .muted{color:{{muted}}}
            h1{margin:0;font-size:20pt;font-weight:800;letter-spacing:-.01em;text-align:right}
            .meta{text-align:right}
            .to{margin:6mm 0 4mm}
            .to .label,.summary dt{font-size:7.5pt;text-transform:uppercase;letter-spacing:.06em;color:{{muted}};font-weight:700}
            section{margin-top:6mm;break-inside:auto}
            h2{font-size:11pt;margin:0 0 2mm;font-weight:800}
            .summary{display:grid;grid-template-columns:repeat(5,1fr);gap:2mm;margin:0 0 3mm;padding:3mm;border:0.3mm solid {{muted}};border-radius:2mm}
            .summary dd{margin:0;font-weight:600}
            .summary .due dd{font-size:11pt;font-weight:800}
            table{width:100%;border-collapse:collapse;table-layout:fixed}
            col.d{width:17mm}col.r{width:30mm}col.m{width:22mm}col.p{width:25mm}col.b{width:23mm}
            td.nw,th.nw{white-space:nowrap}
            td.ref{font-size:8pt;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
            thead{display:table-header-group}
            th{font-size:7.5pt;text-transform:uppercase;letter-spacing:.05em;text-align:left;color:{{muted}};border-bottom:0.4mm solid {{ink}};padding:1.5mm 1.5mm}
            td{padding:1.6mm 1.5mm;border-bottom:0.2mm solid {{muted}};vertical-align:top}
            tr{break-inside:avoid}
            .num{text-align:right;white-space:nowrap}
            .open td,.close td{font-weight:700}
            .empty{padding:6mm 0}
            footer{margin-top:8mm;padding-top:3mm;border-top:0.3mm solid {{muted}};font-size:8.5pt;break-inside:avoid}
            footer p{margin:0 0 2mm;white-space:pre-line}
            """);
        sb.Append("</style></head><body>");

        sb.Append("<header><div class=\"biz\">");
        if (b.LogoDataUri is { } logo && logo.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<img alt=\"\" src=\"").Append(Enc(logo)).Append("\">");
        }

        sb.Append("<strong>").Append(Enc(b.TradingName)).Append("</strong>");
        if (!string.IsNullOrWhiteSpace(b.RegisteredName) && b.RegisteredName != b.TradingName)
        {
            sb.Append("<div class=\"muted\">").Append(Enc(b.RegisteredName)).Append("</div>");
        }

        sb.Append("<div class=\"muted\" style=\"white-space:pre-line\">").Append(Enc(b.Address)).Append("</div>");
        sb.Append("<div class=\"muted\">").Append(Enc(b.Email)).Append(string.IsNullOrWhiteSpace(b.Phone) ? string.Empty : " · " + Enc(b.Phone)).Append("</div>");
        sb.Append("</div><div class=\"meta\"><h1>Statement</h1>");
        sb.Append("<div>").Append(Enc(Date(statement.From))).Append(" – ").Append(Enc(Date(statement.To))).Append("</div>");
        sb.Append("<div class=\"muted\">Issued ").Append(Enc(Date(statement.IssuedOn))).Append("</div></div></header>");

        sb.Append("<div class=\"to\"><div class=\"label\">Statement for</div><strong>").Append(Enc(c.Name)).Append("</strong>");
        if (!string.IsNullOrWhiteSpace(c.Address))
        {
            sb.Append("<div class=\"muted\" style=\"white-space:pre-line\">").Append(Enc(c.Address)).Append("</div>");
        }

        sb.Append("</div>");

        if (statement.Sections.Count == 0)
        {
            sb.Append("<p class=\"empty\">No invoices, credit notes or payments up to ").Append(Enc(Date(statement.To))).Append(".</p>");
        }

        foreach (var s in statement.Sections)
        {
            var cur = s.Currency == "ZWG" ? "ZiG" : s.Currency;
            sb.Append("<section><h2>").Append(Enc(cur)).Append(" account</h2>");
            sb.Append("<dl class=\"summary\">");
            Summary(sb, "Opening balance", Money(s.Opening, s.Currency), false);
            Summary(sb, "Invoiced", Money(s.Invoiced, s.Currency), false);
            Summary(sb, "Credit notes", Money(s.Credited, s.Currency), false);
            Summary(sb, "Payments", Money(s.Paid, s.Currency), false);
            Summary(sb, s.Closing < 0m ? "In credit" : "Amount due", Money(Math.Abs(s.Closing), s.Currency), true);
            sb.Append("</dl>");

            sb.Append("<table><colgroup><col class=\"d\"><col><col class=\"r\"><col class=\"m\"><col class=\"p\"><col class=\"b\"></colgroup>");
            sb.Append("<thead><tr><th>Date</th><th>Details</th><th>Reference</th><th class=\"num\">Charges</th><th class=\"num\">Paid &amp; credited</th><th class=\"num\">Balance</th></tr></thead><tbody>");
            sb.Append("<tr class=\"open\"><td class=\"nw\">").Append(Enc(Short(statement.From))).Append("</td><td colspan=\"4\">Opening balance</td><td class=\"num mono\">")
                .Append(Enc(Plain(s.Opening, s.Currency))).Append("</td></tr>");
            foreach (var l in s.Lines)
            {
                sb.Append("<tr><td class=\"nw\">").Append(Enc(Short(l.Date))).Append("</td><td>").Append(Enc(l.Details)).Append("</td><td class=\"mono ref\" title=\"").Append(Enc(l.Reference)).Append("\">").Append(Enc(l.Reference))
                    .Append("</td><td class=\"num mono\">").Append(l.Charge == 0m ? string.Empty : Enc(Plain(l.Charge, s.Currency)))
                    .Append("</td><td class=\"num mono\">").Append(l.Credit == 0m ? string.Empty : Enc(Plain(l.Credit, s.Currency)))
                    .Append("</td><td class=\"num mono\">").Append(Enc(Plain(l.Balance, s.Currency))).Append("</td></tr>");
            }

            sb.Append("<tr class=\"close\"><td class=\"nw\">").Append(Enc(Short(statement.To))).Append("</td><td colspan=\"4\">Closing balance</td><td class=\"num mono\">")
                .Append(Enc(Plain(s.Closing, s.Currency))).Append("</td></tr></tbody></table></section>");
        }

        sb.Append("<footer>");
        if (!string.IsNullOrWhiteSpace(b.BankDetails))
        {
            sb.Append("<p><strong>How to pay</strong>\n").Append(Enc(b.BankDetails)).Append("</p>");
        }

        sb.Append("<p class=\"muted\">Each currency is shown on its own and never converted. Please contact ").Append(Enc(b.Email))
            .Append(" if anything here doesn't match your records.</p></footer></body></html>");
        return sb.ToString();
    }

    private static void Summary(StringBuilder sb, string label, string value, bool due) =>
        sb.Append(due ? "<div class=\"due\">" : "<div>").Append("<dt>").Append(Enc(label)).Append("</dt><dd class=\"mono\">").Append(Enc(value)).Append("</dd></div>");

    private string Money(decimal amount, string currency) => format.Money(amount, currency);

    private string Plain(decimal amount, string currency) => format.Money(amount, currency, showCurrency: false);

    private static string Date(DateOnly d) => d.ToString("d MMM yyyy", Inv);

    private static string Short(DateOnly d) => d.ToString("d MMM yy", Inv);

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
