using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Formatting;
using InvoiceBuilder.Application.Templates;
using InvoiceBuilder.Infrastructure.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoiceBuilder.Integration.Tests.Rendering;

public sealed class RendererUnitTests
{
    private static readonly IDisplayFormatter Formatter = new DisplayFormatter("Africa/Harare");
    private static readonly FontEmbedder Fonts = new(FontEmbedder.DefaultDirectory);

    private static HtmlTemplateRenderer Renderer(ILoggerFactory? loggers = null) =>
        new(new BindingResolver(Formatter, NullLogger<BindingResolver>.Instance), Fonts, Formatter, loggers ?? NullLoggerFactory.Instance);

    private static BindingResolver Resolver(ILogger<BindingResolver>? logger = null) => new(Formatter, logger ?? NullLogger<BindingResolver>.Instance);

    [Fact]
    public void Binding_whitelist_unknown_path_renders_empty_and_warns()
    {
        var logger = new CapturingLogger<BindingResolver>();
        var model = StudioBoldSample.Model(1);

        Resolver(logger).Resolve("client.secretNotes", model).ShouldBe(string.Empty);
        Resolver(logger).Resolve("document.__proto__", model).ShouldBe(string.Empty);

        logger.Warnings.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("client.name", "Mutare Roofing & Tiles")]
    [InlineData("document.number", "INV-0150")]
    [InlineData("document.total", "$40.00")]
    [InlineData("document.issueDate", "23 Oct 2026")]
    public void Binding_whitelisted_paths_resolve_with_ds52_formats(string path, string expected) =>
        Resolver().Resolve(path, StudioBoldSample.Model(1)).ShouldBe(expected);

    [Fact]
    public async Task Bound_values_are_html_encoded()
    {
        var model = StudioBoldSample.Model(1, clientName: "<script>alert(1)</script>");

        var html = await Renderer().RenderHtmlAsync(StudioBoldSample.WithFooter(), model);

        html.ShouldNotContain("<script>alert(1)</script>");
        html.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public async Task Line_descriptions_and_literal_text_are_html_encoded()
    {
        var model = StudioBoldSample.Model(2, longDescription: "<img src=x onerror=alert(1)>");

        var html = await Renderer().RenderHtmlAsync(StudioBoldSample.WithFooter(), model);

        html.ShouldNotContain("<img src=x");
        html.ShouldContain("&lt;img src=x onerror=alert(1)&gt;");
    }

    [Fact]
    public async Task Rendering_is_deterministic()
    {
        var a = await Renderer().RenderHtmlAsync(StudioBoldSample.WithFooter(), StudioBoldSample.Model(5));
        var b = await Renderer().RenderHtmlAsync(StudioBoldSample.WithFooter(), StudioBoldSample.Model(5));

        a.ShouldBe(b);
    }

    [Fact]
    public void Token_fallback_unknown_token_uses_ink_and_warns()
    {
        var logger = new CapturingLogger<ThemeTokenResolver>();
        var theme = new ThemeTokenResolver(StudioBoldSample.Tokens, logger);

        theme.Color("nonexistent").ShouldBe("#111111");
        logger.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void Token_fallback_paper_fallback_and_hex_and_token_resolution()
    {
        var theme = new ThemeTokenResolver(StudioBoldSample.Tokens, NullLogger.Instance);

        theme.Color("accent").ShouldBe("#F59E0B");
        theme.Color("#abcdef").ShouldBe("#abcdef");
        theme.Color("nonexistent", "paper").ShouldBe("#FFFFFF");
        theme.Color(null).ShouldBe("#111111");
    }

    [Fact]
    public void Token_fallback_rejects_css_injection_in_colour_and_font()
    {
        var theme = new ThemeTokenResolver(StudioBoldSample.Tokens, NullLogger.Instance);

        theme.Color("#fff;background:url(http://evil)").ShouldBe("#111111");
        theme.FontFamily("x';}</style><script>").ShouldBe("Manrope");
    }

    [Fact]
    public async Task Output_has_a_content_security_policy_and_no_script_or_remote_urls()
    {
        var html = await Renderer().RenderHtmlAsync(StudioBoldSample.WithFooter(), StudioBoldSample.Model(3));

        html.ShouldContain("Content-Security-Policy");
        html.ShouldNotContain("<script");
        html.ShouldNotContain("http://fonts");
        html.ShouldNotContain("@import");
    }

    [Fact]
    public async Task Hidden_elements_are_not_rendered()
    {
        var definition = StudioBoldSample.Definition with
        {
            FirstPage = new PageLayer { Elements = [new TemplateElement { Id = "h", Text = "SHOULD-NOT-APPEAR", Hidden = true, WidthMm = 10, HeightMm = 5 }] },
        };

        var html = await Renderer().RenderHtmlAsync(definition, StudioBoldSample.Model(1));

        html.ShouldNotContain("SHOULD-NOT-APPEAR");
    }

    [Fact]
    public async Task Instalment_schedule_with_plan_of_3_renders_3_rows()
    {
        var definition = StudioBoldSample.Definition with
        {
            Flow = StudioBoldSample.Definition.Flow with { InstalmentSchedule = new InstalmentScheduleFlow() },
        };
        var rows = Enumerable.Range(1, 3)
            .Select(i => new InstalmentRenderRow(i, new DateOnly(2026, 11, i), 13.33m, "Upcoming", 13.33m))
            .ToList();
        var model = StudioBoldSample.Model(1) with { Plan = new PlanRenderInfo(3, 1, 13.33m, new DateOnly(2026, 11, 1), 0m) { Rows = rows } };

        var html = await Renderer().RenderHtmlAsync(definition, model);

        System.Text.RegularExpressions.Regex.Count(html, "class=\"inst\"").ShouldBe(3);
    }

    [Fact]
    public async Task Instalment_schedule_without_plan_renders_nothing()
    {
        var definition = StudioBoldSample.Definition with
        {
            Flow = StudioBoldSample.Definition.Flow with { InstalmentSchedule = new InstalmentScheduleFlow() },
        };

        var html = await Renderer().RenderHtmlAsync(definition, StudioBoldSample.Model(1));

        html.ShouldNotContain("class=\"schedule\"");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
