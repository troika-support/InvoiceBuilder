using InvoiceBuilder.Application.Templates;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Templates;

public sealed class LogoPaletteTests
{
    [Fact]
    public void Svg_colours_are_extracted_most_common_first()
    {
        var svg = "<svg><rect fill=\"#0F766E\"/><rect fill=\"#0F766E\"/><circle fill=\"#F59E0B\"/><path fill=\"#fff\"/></svg>"u8.ToArray();
        var palette = LogoPalette.Extract(svg, "image/svg+xml");
        palette[0].ShouldBe("#007766");
        palette.Count.ShouldBe(2);
    }

    [Fact]
    public void Unreadable_png_gives_no_colours()
    {
        LogoPalette.Extract([1, 2, 3], "image/png").ShouldBeEmpty();
    }
}
