using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Formatting;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Infrastructure.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace InvoiceBuilder.Integration.Tests.Rendering;

public sealed class PdfFixture : IAsyncLifetime, IAsyncDisposable
{
    private static readonly IDisplayFormatter Formatter = new DisplayFormatter("Africa/Harare");

    private readonly PlaywrightBrowserPool _pool = new(Options.Create(new PdfOptions()), NullLogger<PlaywrightBrowserPool>.Instance);

    public PlaywrightPdfRenderer Pdf => new(_pool, Options.Create(new PdfOptions()));

    public HtmlTemplateRenderer Html { get; } = new(
        new BindingResolver(Formatter, NullLogger<BindingResolver>.Instance),
        new FontEmbedder(FontEmbedder.DefaultDirectory),
        Formatter,
        NullLoggerFactory.Instance);

    public async Task<PdfDocument> RenderAsync(int lines, string? longDescription = null)
    {
        var html = await Html.RenderHtmlAsync(StudioBoldSample.WithFooter(), StudioBoldSample.Model(lines, longDescription: longDescription));
        return PdfDocument.Open(await Pdf.RenderAsync(html));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _pool.DisposeAsync();

    async ValueTask IAsyncDisposable.DisposeAsync() => await _pool.DisposeAsync();

    public static string Text(Page page) => string.Join(' ', page.GetWords().Select(w => w.Text));
}
