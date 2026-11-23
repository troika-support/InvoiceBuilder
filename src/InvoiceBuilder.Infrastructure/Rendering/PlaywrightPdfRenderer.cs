using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Application.Templates;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>HTML to PDF through pooled Chromium (template-rendering.md §5). No network, 20 s default timeout, backgrounds printed.</summary>
public sealed class PlaywrightPdfRenderer(IBrowserPool pool, IOptions<PdfOptions> options) : IPdfRenderer
{
    public async Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);

        var timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        await using var lease = await pool.RentAsync(timeoutSource.Token);
        await using var context = await lease.Browser.NewContextAsync(new BrowserNewContextOptions { JavaScriptEnabled = false });
        context.SetDefaultTimeout((float)timeout.TotalMilliseconds);

        // Pages can only use data: URIs; anything else, including http(s) and file, is refused.
        await context.RouteAsync("**/*", route =>
            route.Request.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? route.ContinueAsync() : route.AbortAsync());

        var page = await context.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });
        await page.EvaluateAsync("document.fonts.ready");

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            PreferCSSPageSize = true,
            Margin = new Margin { Top = "0", Right = "0", Bottom = "0", Left = "0" },
        });
    }
}
