using InvoiceBuilder.Application.Templates;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Templates;

public sealed class ThemeContrastTests
{
    [Fact]
    public void Black_on_white_is_21_to_1()
    {
        ThemeContrast.Ratio("#000000", "#FFFFFF")!.Value.ShouldBe(21d, 0.01);
    }

    [Fact]
    public void Troika_ink_on_accent_passes()
    {
        ThemeContrast.Ratio("#111111", "#F59E0B")!.Value.ShouldBeGreaterThan(8);
    }

    [Fact]
    public void Below_3_fails_and_between_3_and_4_5_warns()
    {
        var tokens = new Dictionary<string, string> { ["ink"] = "#CCCCCC", ["paper"] = "#FFFFFF", ["muted"] = "#8A8A8A" };
        var result = ThemeContrast.Check(tokens);
        result.Single(r => r.Text == "ink").Level.ShouldBe(ContrastLevel.Fail);
        result.Single(r => r.Text == "muted").Level.ShouldBe(ContrastLevel.Warn);
    }
}
