using System.Text.RegularExpressions;
using Serilog.Context;

namespace InvoiceBuilder.Web.Logging;

/// <summary>
/// Gives every request an id that appears on all its log lines and in the X-Correlation-ID response header, so a
/// problem a user reports can be found in the logs. A well-formed incoming header (e.g. from IIS/ARR) is reused.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var incoming = context.Request.Headers[HeaderName].ToString();
        var id = Valid().IsMatch(incoming) ? incoming : context.TraceIdentifier;
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$")]
    private static partial Regex Valid();
}
