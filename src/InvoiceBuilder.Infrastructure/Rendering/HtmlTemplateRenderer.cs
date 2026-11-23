using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Templates;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>
/// Designer JSON to HTML (template-rendering.md §4). A pure function of (definition, model): no clock, no I/O.
/// Every bound value is HTML-encoded, and every style value is a validated number, hex colour or whitelisted word.
/// </summary>
public sealed partial class HtmlTemplateRenderer(
    BindingResolver bindings,
    FontEmbedder fonts,
    IDisplayFormatter formatter,
    ILoggerFactory loggerFactory) : ITemplateRenderer
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<HtmlTemplateRenderer>();

    // Defence in depth: even if something slipped through, the page can't run script or fetch anything but data: URIs.
    private const string ContentSecurityPolicy = "default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'";

    public Task<string> RenderHtmlAsync(TemplateDefinition definition, DocumentRenderModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(model);

        return Task.FromResult(Render(definition, model));
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Mm(double value) => N(value) + "mm";

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Align(string? value) => value switch
    {
        "center" => "center",
        "right" => "right",
        _ => "left",
    };

    private string Render(TemplateDefinition d, DocumentRenderModel m)
    {
        var theme = new ThemeTokenResolver(m.ThemeTokens, loggerFactory.CreateLogger<ThemeTokenResolver>());
        var sb = new StringBuilder(32 * 1024);

        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"").Append(ContentSecurityPolicy).Append("\">");
        sb.Append("<title>").Append(Enc(m.Document.Number)).Append("</title><style>");
        sb.Append(fonts.FontFaceCss);
        AppendCss(sb, d, theme);
        sb.Append("</style></head><body>");

        // Continuation-page elements are fixed (they repeat) and sit under the first-page layer, whose paper-coloured
        // background hides them on page 1. repeat:"all" elements sit above that layer so they show on every page.
        var continuation = d.ContinuationPage.Elements.Where(e => !e.Hidden && e.Repeat != RepeatModes.All).OrderBy(e => e.ZIndex).ToList();
        foreach (var element in continuation)
        {
            AppendElement(sb, element, m, theme, "el fx fx-below");
        }

        sb.Append("<div class=\"first-layer\">");
        foreach (var element in d.FirstPage.Elements.Where(e => !e.Hidden && e.Repeat != RepeatModes.All).OrderBy(e => e.ZIndex))
        {
            AppendElement(sb, element, m, theme, "el");
        }

        sb.Append("</div>");

        var repeating = d.FirstPage.Elements.Concat(d.ContinuationPage.Elements)
            .Where(e => !e.Hidden && e.Repeat == RepeatModes.All)
            .OrderBy(e => e.ZIndex);
        foreach (var element in repeating)
        {
            AppendElement(sb, element, m, theme, "el fx fx-above");
        }

        AppendFlow(sb, d, m, theme);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendCss(StringBuilder sb, TemplateDefinition d, ThemeTokenResolver theme)
    {
        var paper = theme.Color("paper", "paper");
        var ink = theme.Color("ink");
        var width = N(Math.Clamp(d.Page.WidthMm, 50, 1000));
        var height = N(Math.Clamp(d.Page.HeightMm, 50, 1500));
        var layerHeight = N(Math.Clamp(d.Page.HeightMm, 50, 1500) - 0.8);

        sb.Append(CultureInfo.InvariantCulture, $"@page{{size:{width}mm {height}mm;margin:0}}");
        sb.Append("*{box-sizing:border-box}html,body{margin:0;padding:0}");
        sb.Append(CultureInfo.InvariantCulture, $"body{{display:flow-root;background:{paper};color:{ink};font-family:'{theme.FontFamily("body")}',sans-serif;font-size:9pt;-webkit-print-color-adjust:exact;print-color-adjust:exact}}");
        sb.Append(".el{position:absolute;overflow-wrap:anywhere}.fx{position:fixed}.fx-below{z-index:0}.fx-above{z-index:2}");
        sb.Append(CultureInfo.InvariantCulture, $".first-layer{{position:absolute;left:0;top:0;width:{width}mm;height:{layerHeight}mm;background:{paper};z-index:1}}");
        sb.Append(".qr svg{display:block;width:100%;height:100%}");
        sb.Append(".flow{position:relative;z-index:3}");
        sb.Append("table.items{width:100%;border-collapse:collapse;table-layout:fixed}");
        sb.Append("table.items thead{display:table-header-group}table.items tfoot{display:table-footer-group}");
        sb.Append("table.items tbody tr{break-inside:avoid}");
        sb.Append("table.items td,table.items th{padding:1.1mm 2mm;vertical-align:top;overflow-wrap:anywhere}");
        sb.Append("table.items .num{text-align:right}table.items tr.spacer th,table.items tr.reserve td{padding:0;border:0}");
        sb.Append(".totals{break-inside:avoid}.trow{display:flex;justify-content:space-between;gap:4mm;padding:0.8mm 0}");
        sb.Append(CultureInfo.InvariantCulture, $".mono{{font-family:'{theme.FontFamily("mono")}',monospace;font-variant-numeric:tabular-nums}}");
        sb.Append(".detail{display:block;font-size:8pt}");
    }

    private static string StyleCss(TextStyle s, ThemeTokenResolver theme, string fallbackColor = "ink")
    {
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $"font-family:'{theme.FontFamily(s.Font)}',sans-serif;font-size:{N(Math.Clamp(s.SizePt, 4, 120))}pt;font-weight:{Math.Clamp(s.Weight, 100, 900)};color:{theme.Color(s.Color, fallbackColor)};");
        if (s.Uppercase)
        {
            css.Append("text-transform:uppercase;");
        }

        if (s.LetterSpacing != 0)
        {
            css.Append(CultureInfo.InvariantCulture, $"letter-spacing:{N(Math.Clamp(s.LetterSpacing, -1, 2))}em;");
        }

        return css.ToString();
    }

    private void AppendElement(StringBuilder sb, TemplateElement e, DocumentRenderModel m, ThemeTokenResolver theme, string cssClass)
    {
        var box = $"left:{Mm(e.XMm)};top:{Mm(e.YMm)};width:{Mm(e.WidthMm)};height:{Mm(e.HeightMm)};";
        if (e.Type != ElementTypes.Line && e.Rotation != 0)
        {
            box += $"transform:rotate({e.Rotation.ToString(CultureInfo.InvariantCulture)}deg);transform-origin:center;";
        }

        if (e.Opacity is > 0 and < 1)
        {
            box += $"opacity:{N(e.Opacity)};";
        }


        switch (e.Type)
        {
            case ElementTypes.Text:
            case ElementTypes.Field:
            case ElementTypes.Signature:
                var content = e.Type == ElementTypes.Text
                    ? Expand(e.Text, m)
                    : Enc(bindings.Resolve(e.Binding, m, e.Format)).Replace("\n", "<br>", StringComparison.Ordinal);
                sb.Append(CultureInfo.InvariantCulture, $"<div class=\"{cssClass}\" style=\"{box}text-align:{Align(e.Align)};line-height:{N(Math.Clamp(e.LineHeight, 0.8, 3))};");
                sb.Append(StyleCss(new TextStyle { Font = e.Font, SizePt = e.SizePt, Weight = e.Weight, Color = e.Color, Uppercase = e.Uppercase, LetterSpacing = e.LetterSpacing }, theme));
                sb.Append("\">").Append(content).Append("</div>");
                break;

            case ElementTypes.Shape:
                var radius = e.Shape == "ellipse" ? "50%" : Mm(Math.Max(0, e.RadiusMm));
                var fill = e.Fill is null ? "transparent" : theme.Color(e.Fill, "paper");
                var border = e.StrokeWidthMm > 0 ? $"border:{Mm(e.StrokeWidthMm)} solid {theme.Color(e.Stroke)};" : string.Empty;
                sb.Append(CultureInfo.InvariantCulture, $"<div class=\"{cssClass}\" style=\"{box}background:{fill};border-radius:{radius};{border}\"></div>");
                break;

            case ElementTypes.Line:
                var minX = Math.Min(e.XMm, e.X2Mm);
                var minY = Math.Min(e.YMm, e.Y2Mm);
                var pad = Math.Max(e.StrokeWidthMm, 0.1);
                var w = Math.Abs(e.X2Mm - e.XMm) + (2 * pad);
                var h = Math.Abs(e.Y2Mm - e.YMm) + (2 * pad);
                sb.Append(CultureInfo.InvariantCulture, $"<svg class=\"{cssClass}\" style=\"left:{Mm(minX - pad)};top:{Mm(minY - pad)};width:{Mm(w)};height:{Mm(h)};overflow:visible\" viewBox=\"0 0 {N(w)} {N(h)}\">");
                sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{N(e.XMm - minX + pad)}\" y1=\"{N(e.YMm - minY + pad)}\" x2=\"{N(e.X2Mm - minX + pad)}\" y2=\"{N(e.Y2Mm - minY + pad)}\" stroke=\"{theme.Color(e.Stroke)}\" stroke-width=\"{N(pad)}\"{DashAttr(e)}/></svg>");
                break;

            case ElementTypes.Image:
                if (BindingResolver.ResolveImage(e.Binding, m) is { } src)
                {
                    var fit = e.Fit == "cover" ? "cover" : "contain";
                    sb.Append(CultureInfo.InvariantCulture, $"<img class=\"{cssClass}\" alt=\"\" src=\"{Enc(src)}\" style=\"{box}object-fit:{fit}\">");
                }

                break;

            case ElementTypes.PayQr:
                if (!string.IsNullOrWhiteSpace(m.Document.ClientLinkUrl))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"<div class=\"{cssClass} qr\" role=\"img\" aria-label=\"QR code to pay online\" style=\"{box}\">");
                    sb.Append(QrSvg(m.Document.ClientLinkUrl, theme.Color(e.Fill), theme.Color(e.Color, "paper"))).Append("</div>");
                }

                break;

            default:
                LogUnknownElementType(e.Type);
                break;
        }
    }

    private static string DashAttr(TemplateElement e)
    {
        var w = Math.Max(e.StrokeWidthMm, 0.1);
        return e.Dash switch
        {
            "dashed" => string.Create(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{w * 6:0.##} {w * 3:0.##}\""),
            "dotted" => string.Create(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{w:0.##} {w * 2:0.##}\" stroke-linecap=\"round\""),
            _ => string.Empty,
        };
    }

    private static string QrSvg(string url, string dark, string light)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4, dark, light, drawQuietZones: false, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    /// <summary>Literal text and bound values are each HTML-encoded; only the {{path}} markers are interpreted.</summary>
    private string Expand(string? template, DocumentRenderModel m)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var last = 0;
        foreach (var match in BindingMarker().EnumerateMatches(template))
        {
            sb.Append(Enc(template[last..match.Index]));
            var path = template.Substring(match.Index + 2, match.Length - 4).Trim();
            sb.Append(Enc(bindings.Resolve(path, m)));
            last = match.Index + match.Length;
        }

        sb.Append(Enc(template[last..]));
        return sb.Replace("\n", "<br>").ToString();
    }

    private void AppendFlow(StringBuilder sb, TemplateDefinition d, DocumentRenderModel m, ThemeTokenResolver theme)
    {
        var flow = d.Flow;
        var table = flow.ItemsTable;
        var cols = table.Columns;
        var sum = cols.Description + cols.Qty + cols.Rate + cols.Tax + cols.Amount;
        if (sum <= 0)
        {
            sum = 100;
        }

        string Width(double c) => N(c / sum * 100) + "%";
        var top = Math.Max(d.ContinuationPage.ItemsTop, 0);
        var marginTop = Math.Max(table.YMm - top, 0);
        var rule = "#E5E5E5";

        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"flow\" style=\"margin-left:{Mm(table.XMm)};margin-top:{Mm(marginTop)};width:{Mm(table.WidthMm)}\">");
        sb.Append("<table class=\"items\"><colgroup>");
        foreach (var c in new[] { cols.Description, cols.Qty, cols.Rate, cols.Tax, cols.Amount })
        {
            sb.Append(CultureInfo.InvariantCulture, $"<col style=\"width:{Width(c)}\">");
        }

        sb.Append("</colgroup><thead>");
        sb.Append(CultureInfo.InvariantCulture, $"<tr class=\"spacer\"><th colspan=\"5\" style=\"height:{Mm(top)}\"></th></tr>");

        var headerStyle = StyleCss(table.Header, theme);
        var headerFill = table.Header.Fill is null ? string.Empty : $"background:{theme.Color(table.Header.Fill, "paper")};";
        var headerRule = $"border-bottom:0.3mm solid {theme.Color(table.Header.BorderColor, "ink")};";
        sb.Append("<tr class=\"hdr\">");
        AppendHeaderCell(sb, "Description", string.Empty, headerStyle + headerFill + headerRule);
        AppendHeaderCell(sb, "Qty", "num", headerStyle + headerFill + headerRule);
        AppendHeaderCell(sb, "Rate", "num", headerStyle + headerFill + headerRule);
        AppendHeaderCell(sb, "Tax", "num", headerStyle + headerFill + headerRule);
        AppendHeaderCell(sb, "Amount", "num", headerStyle + headerFill + headerRule);
        sb.Append("</tr></thead><tbody>");

        var rowStyle = StyleCss(table.Row, theme);
        var zebra = theme.Color("clientSurface", "paper");
        for (var i = 0; i < m.Lines.Count; i++)
        {
            var line = m.Lines[i];
            var last = i == m.Lines.Count - 1;
            var background = table.Zebra && i % 2 == 1 ? $"background:{zebra};" : string.Empty;
            var keep = last ? "break-after:avoid;" : string.Empty;
            sb.Append(CultureInfo.InvariantCulture, $"<tr style=\"{rowStyle}{background}{keep}min-height:{Mm(table.RowHeightMm)}\">");
            sb.Append(CultureInfo.InvariantCulture, $"<td style=\"border-bottom:0.2mm solid {rule}\">{Enc(line.Description)}");
            if (table.ShowDetailLine && !string.IsNullOrWhiteSpace(line.Detail))
            {
                sb.Append("<span class=\"detail\" style=\"color:").Append(theme.Color("muted")).Append("\">").Append(Enc(line.Detail)).Append("</span>");
            }

            sb.Append("</td>");
            AppendNumberCell(sb, line.Quantity.ToString("0.##", CultureInfo.InvariantCulture), rule);
            AppendNumberCell(sb, formatter.Money(line.Rate, m.Document.Currency, showCurrency: false), rule);
            AppendNumberCell(sb, line.TaxLabel, rule);
            AppendNumberCell(sb, formatter.Money(line.Amount, m.Document.Currency, showCurrency: false), rule);
            sb.Append("</tr>");
        }

        AppendTotals(sb, d, m, theme);
        sb.Append("</tbody>");
        sb.Append(CultureInfo.InvariantCulture, $"<tfoot><tr class=\"reserve\"><td colspan=\"5\" style=\"height:{Mm(Math.Max(flow.FooterReserveMm, 0))}\"></td></tr></tfoot>");
        sb.Append("</table></div>");
    }

    private static void AppendHeaderCell(StringBuilder sb, string label, string cssClass, string style) =>
        sb.Append(CultureInfo.InvariantCulture, $"<th class=\"{cssClass}\" style=\"{style}text-align:{(cssClass == "num" ? "right" : "left")}\">{label}</th>");

    private static void AppendNumberCell(StringBuilder sb, string text, string rule) =>
        sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num mono\" style=\"border-bottom:0.2mm solid {rule}\">{Enc(text)}</td>");

    private void AppendTotals(StringBuilder sb, TemplateDefinition d, DocumentRenderModel m, ThemeTokenResolver theme)
    {
        var totals = d.Flow.Totals;
        var doc = m.Document;
        var currency = doc.Currency;

        sb.Append("<tr class=\"totals-row\"><td colspan=\"5\" style=\"padding:0;border:0\">");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"totals\" style=\"margin-top:{Mm(totals.GapMm)};margin-left:{Mm(totals.XMm - d.Flow.ItemsTable.XMm)};width:{Mm(totals.WidthMm)};{StyleCss(totals.Style, theme)}\">");
        TotalRow(sb, "Subtotal", formatter.Money(doc.Subtotal, currency), bold: false);
        if (doc.DiscountTotal > 0)
        {
            TotalRow(sb, "Discount", formatter.Money(-doc.DiscountTotal, currency), bold: false);
        }

        if (doc.TaxTotal > 0)
        {
            TotalRow(sb, "Tax", formatter.Money(doc.TaxTotal, currency), bold: false);
        }

        TotalRow(sb, $"Total {currency}", formatter.Money(doc.Total, currency), bold: true);
        if (doc.AmountPaid > 0)
        {
            TotalRow(sb, "Paid", formatter.Money(doc.AmountPaid, currency), bold: false);
            TotalRow(sb, "Balance due", formatter.Money(doc.Balance, currency), bold: true);
        }

        sb.Append("</div></td></tr>");
        AppendSchedule(sb, d, m, theme);
    }

    private void AppendSchedule(StringBuilder sb, TemplateDefinition d, DocumentRenderModel m, ThemeTokenResolver theme)
    {
        var schedule = d.Flow.InstalmentSchedule;
        if (schedule is null || m.Plan is not { Rows.Count: > 0 } plan)
        {
            return;
        }

        var currency = m.Document.Currency;
        sb.Append("<tr class=\"schedule-row\"><td colspan=\"5\" style=\"padding:0;border:0\">");
        sb.Append(CultureInfo.InvariantCulture, $"<table class=\"schedule\" style=\"margin-top:{Mm(schedule.GapMm)};margin-left:{Mm(schedule.XMm - d.Flow.ItemsTable.XMm)};width:{Mm(schedule.WidthMm)};border-collapse:collapse;break-inside:avoid\"><thead><tr>");
        foreach (var heading in new[] { "No.", "Due", "Amount", "Status", "Remaining" })
        {
            sb.Append(CultureInfo.InvariantCulture, $"<th style=\"text-align:left;font-size:8pt;text-transform:uppercase;padding:1mm 2mm;border-bottom:0.3mm solid {theme.Color("ink")}\">{heading}</th>");
        }

        sb.Append("</tr></thead><tbody>");
        foreach (var row in plan.Rows)
        {
            var due = row.DueDate is { } date ? formatter.Date(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)) : string.Empty;
            sb.Append(CultureInfo.InvariantCulture, $"<tr class=\"inst\"><td>{row.Number}</td><td>{Enc(due)}</td><td class=\"mono\">{Enc(formatter.Money(row.Amount, currency))}</td><td>{Enc(row.Status)}</td><td class=\"mono\">{Enc(formatter.Money(row.Remaining, currency))}</td></tr>");
        }

        sb.Append("</tbody></table></td></tr>");
    }

    private static void TotalRow(StringBuilder sb, string label, string value, bool bold) =>
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"trow\" style=\"font-weight:{(bold ? 700 : 400)}\"><span>{Enc(label)}</span><span class=\"mono\">{Enc(value)}</span></div>");
    [GeneratedRegex(@"\{\{\s*[A-Za-z0-9_.]+\s*\}\}")]
    private static partial Regex BindingMarker();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown element type {ElementType} skipped")]
    private partial void LogUnknownElementType(string elementType);
}
