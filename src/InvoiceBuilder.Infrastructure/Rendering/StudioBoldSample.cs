using InvoiceBuilder.Application.Templates;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>
/// Hand-written "Studio Bold" template (canvas 3) and sample data. Used by the dev render endpoint and the PDF tests until the
/// designer produces real definitions.
/// </summary>
// VERIFY: positions are a hand-written approximation of canvas 3; compare with the canvas image when it is available.
public static class StudioBoldSample
{
    public static IReadOnlyDictionary<string, string> Tokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ink"] = "#111111",
        ["accent"] = "#F59E0B",
        ["paper"] = "#FFFFFF",
        ["muted"] = "#6B6B6B",
        ["clientSurface"] = "#F5F5F4",
        ["fonts.body"] = "Manrope",
        ["fonts.mono"] = "IBM Plex Mono",
    };

    public static TemplateDefinition Definition { get; } = new()
    {
        ThemeTokens = ["ink", "accent", "paper", "muted"],
        FirstPage = new PageLayer
        {
            Elements =
            [
                new() { Id = "band", Type = ElementTypes.Shape, XMm = 0, YMm = 0, WidthMm = 210, HeightMm = 14, Fill = "accent", ZIndex = 0 },
                new() { Id = "brand", Type = ElementTypes.Field, Binding = "business.tradingName", XMm = 18, YMm = 22, WidthMm = 100, HeightMm = 10, SizePt = 20, Weight = 800 },
                new() { Id = "title", Type = ElementTypes.Field, Binding = "document.typeLabel", XMm = 120, YMm = 22, WidthMm = 72, HeightMm = 12, SizePt = 26, Weight = 800, Align = "right", Uppercase = true },
                new() { Id = "number", Type = ElementTypes.Field, Binding = "document.number", XMm = 120, YMm = 35, WidthMm = 72, HeightMm = 6, SizePt = 10, Font = "mono", Align = "right" },
                new() { Id = "billedLabel", Type = ElementTypes.Text, Text = "Billed to", XMm = 18, YMm = 50, WidthMm = 80, HeightMm = 5, SizePt = 8, Weight = 700, Color = "muted", Uppercase = true, LetterSpacing = 0.06 },
                new() { Id = "client", Type = ElementTypes.Field, Binding = "client.name", XMm = 18, YMm = 56, WidthMm = 90, HeightMm = 7, SizePt = 12, Weight = 700 },
                new() { Id = "clientAddress", Type = ElementTypes.Field, Binding = "client.address", XMm = 18, YMm = 63, WidthMm = 90, HeightMm = 10, SizePt = 9, Color = "muted" },
                new() { Id = "issued", Type = ElementTypes.Text, Text = "Issued {{document.issueDate}}", XMm = 120, YMm = 56, WidthMm = 72, HeightMm = 5, SizePt = 9, Align = "right" },
                new() { Id = "due", Type = ElementTypes.Text, Text = "Due {{document.dueDate}}", XMm = 120, YMm = 62, WidthMm = 72, HeightMm = 5, SizePt = 9, Align = "right" },
            ],
        },
        ContinuationPage = new ContinuationLayer
        {
            ItemsTop = 24,
            Elements =
            [
                new() { Id = "contBand", Type = ElementTypes.Shape, XMm = 0, YMm = 0, WidthMm = 210, HeightMm = 6, Fill = "accent", Repeat = RepeatModes.All },
                new() { Id = "contTitle", Type = ElementTypes.Text, Text = "{{business.tradingName}} · {{document.number}}", XMm = 18, YMm = 11, WidthMm = 174, HeightMm = 6, SizePt = 9, Weight = 700, Repeat = RepeatModes.All },
            ],
        },
        Flow = new FlowDefinition
        {
            FooterReserveMm = 30,
            ItemsTable = new ItemsTableElement
            {
                XMm = 18,
                YMm = 88,
                WidthMm = 174,
                Columns = new ItemsColumns { Description = 52, Qty = 8, Rate = 15, Tax = 8, Amount = 17 },
                Header = new TextStyle { SizePt = 8, Weight = 700, Uppercase = true, LetterSpacing = 0.06, Color = "paper", Fill = "ink", BorderColor = "ink" },
                Row = new TextStyle { SizePt = 9 },
                ShowDetailLine = false,
            },
            Totals = new TotalsFlow { GapMm = 10, XMm = 108, WidthMm = 84, Style = new TextStyle { SizePt = 10 } },
        },
    };

    /// <summary>Footer elements are on every page: they sit above the first-page layer and are fixed.</summary>
    public static TemplateDefinition WithFooter() => Definition with
    {
        FirstPage = new PageLayer
        {
            Elements =
            [
                .. Definition.FirstPage.Elements,
                .. Footer(),
            ],
        },
    };

    private static IEnumerable<TemplateElement> Footer() =>
    [
        new() { Id = "footLine", Type = ElementTypes.Line, XMm = 18, YMm = 270, X2Mm = 192, Y2Mm = 270, Stroke = "ink", StrokeWidthMm = 0.3, Repeat = RepeatModes.All },
        new() { Id = "pay", Type = ElementTypes.Text, Text = "Pay online: {{document.clientLinkUrl}}", XMm = 18, YMm = 273, WidthMm = 130, HeightMm = 6, SizePt = 9, Weight = 700, Repeat = RepeatModes.All },
        new() { Id = "foot", Type = ElementTypes.Text, Text = "{{business.tradingName}} · {{business.email}}", XMm = 18, YMm = 279, WidthMm = 130, HeightMm = 5, SizePt = 8, Color = "muted", Repeat = RepeatModes.All },
        new() { Id = "qr", Type = ElementTypes.PayQr, Binding = "document.clientLinkUrl", XMm = 168, YMm = 272, WidthMm = 24, HeightMm = 24, Fill = "ink", Color = "paper", Repeat = RepeatModes.All },
    ];

    public static DocumentRenderModel Model(int lines, string clientName = "Mutare Roofing & Tiles", string? longDescription = null)
    {
        var items = Enumerable.Range(1, lines)
            .Select(i => new RenderLine(
                i == 1 && longDescription is not null ? longDescription : $"Design work item {i}",
                $"Phase {(i % 4) + 1} deliverable",
                1,
                40m,
                "—",
                40m))
            .ToList();
        var subtotal = items.Sum(l => l.Amount);

        return new DocumentRenderModel(
            new BusinessRenderModel("Troika Design Workshop", "Troika Design Workshop (Pvt) Ltd", "hello@troika.example", "+263 77 000 0000", "12 Samora Machel Ave\nHarare", null),
            new ClientRenderModel(clientName, "accounts@example.com", "+263 77 000 0001", "4 Main Street\nMutare"),
            new DocumentRenderInfo("Invoice", "INV-0150", new DateOnly(2026, 10, 23), new DateOnly(2026, 11, 6), null, "USD", subtotal, 0m, 0m, subtotal, 0m, subtotal, null, null, "https://example.test/d/sample"),
            items,
            Tokens);
    }
}
