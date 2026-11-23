using UglyToad.PdfPig.Content;

namespace InvoiceBuilder.Integration.Tests.Rendering;

[Trait("Category", "Pdf")]
public sealed class PdfRenderingTests(PdfFixture fixture) : IClassFixture<PdfFixture>
{
    [Fact]
    public async Task Pdf_3_lines_make_1_page()
    {
        using var pdf = await fixture.RenderAsync(3);

        pdf.NumberOfPages.ShouldBe(1);
    }

    [Fact]
    public async Task Pdf_60_lines_make_3_pages_with_the_header_on_every_page()
    {
        using var pdf = await fixture.RenderAsync(60);

        pdf.NumberOfPages.ShouldBe(3);
        foreach (var page in pdf.GetPages())
        {
            CountWord(page, "DESCRIPTION").ShouldBe(1, $"page {page.Number} should repeat the table header once");
        }
    }

    [Fact]
    public async Task Pdf_totals_appear_once_on_the_last_page_after_the_last_line()
    {
        using var pdf = await fixture.RenderAsync(60);
        var pages = pdf.GetPages().ToList();

        pages.Sum(p => CountWord(p, "Subtotal")).ShouldBe(1);
        pages.Take(pages.Count - 1).ShouldAllBe(p => CountWord(p, "Subtotal") == 0);

        var last = pages[^1];
        var lastLine = last.GetWords().Where(w => w.Text == "60").Select(w => w.BoundingBox.Top).DefaultIfEmpty(0).Max();
        var subtotalTop = last.GetWords().Single(w => w.Text == "Subtotal").BoundingBox.Top;
        subtotalTop.ShouldBeLessThan(lastLine, "PDF y grows upwards, so a smaller y means lower on the page");
        CountWord(last, "$2,400.00").ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Pdf_footer_pay_online_appears_on_every_page()
    {
        using var pdf = await fixture.RenderAsync(60);

        foreach (var page in pdf.GetPages())
        {
            PdfFixture.Text(page).ShouldContain("Pay online");
        }
    }

    [Fact]
    public async Task Pdf_500_character_description_stays_within_its_column()
    {
        var longText = string.Join(' ', Enumerable.Repeat("Extensive brand identity workshop deliverables", 12))[..500];
        using var pdf = await fixture.RenderAsync(3, longText);

        var page = pdf.GetPage(1);
        const double mmToPt = 72 / 25.4;
        var rightMargin = (210 - 18) * mmToPt;
        page.GetWords().Max(w => w.BoundingBox.Right).ShouldBeLessThanOrEqualTo(rightMargin + 1, "no text beyond the page's right margin");

        // The description column ends where the Qty column starts: 18 mm + 52/100 of 174 mm.
        var descriptionRight = (18 + (174 * 52 / 100d)) * mmToPt;
        page.GetWords()
            .Where(w => longText.Contains(w.Text, StringComparison.Ordinal) && w.Text.Length > 6)
            .ShouldAllBe(w => w.BoundingBox.Right <= descriptionRight + 1);
    }

    private static int CountWord(Page page, string word) => page.GetWords().Count(w => w.Text == word);
}
