using InvoiceBuilder.Web.Logging;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Serilog.Parsing;

namespace InvoiceBuilder.Integration.Tests.Web;

public sealed class LoggingTests
{
    [Theory]
    [InlineData("GET /d/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abc/pdf", "GET /d/[link]/pdf")]
    [InlineData("/account/reset-password?email=x&token=abc123", "/account/reset-password?email=x&token=[redacted]")]
    [InlineData("Authorization: Bearer eyJhbGciOi.xyz", "Authorization: Bearer [redacted]")]
    [InlineData("sent to masimba@troika.co.zw", "sent to m***@troika.co.zw")]
    [InlineData("WhatsApp to +263 77 123 4567 failed", "WhatsApp to ***67 failed")]
    [InlineData("phone 0771234567", "phone ***67")]
    public void Personal_data_and_secrets_are_masked(string input, string expected) =>
        RedactionEnricher.Mask(input).ShouldBe(expected);

    [Theory]
    [InlineData("INV-0150 issued on 2026-11-23 for 1,200.00")]
    [InlineData("Payment 0198a000-0000-7000-8000-000000000001 confirmed")]
    [InlineData("/invoices/0198a000-0000-7000-8000-000000000001")]
    public void Ordinary_values_are_left_alone(string input) =>
        RedactionEnricher.Mask(input).ShouldBeSameAs(input);

    [Fact]
    public void Enricher_masks_event_properties()
    {
        var evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, new MessageTemplateParser().Parse("{RequestPath}"),
            [new LogEventProperty("RequestPath", new ScalarValue("/d/AbCdEfGhIjKlMnOpQrStUvWxYz0123"))]);

        new RedactionEnricher().Enrich(evt, null!);

        evt.Properties["RequestPath"].ToString().ShouldBe("\"/d/[link]\"");
    }

    [Fact]
    public async Task Correlation_id_reuses_a_valid_header_and_echoes_it()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123";
        string? seen = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            seen = ctx.TraceIdentifier;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        seen.ShouldBe("abc-123");
    }

    [Fact]
    public async Task Correlation_id_ignores_a_malformed_header()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "<script>";
        await new CorrelationIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.TraceIdentifier.ShouldBe("trace-1");
    }
}
