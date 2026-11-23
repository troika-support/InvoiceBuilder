using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace InvoiceBuilder.Web.Logging;

/// <summary>
/// Masks personal data and secrets in log properties before any sink sees them (hardening items 7 and 10):
/// client link tokens in paths (/d/{token}), token and key query values, bearer tokens, email addresses and
/// phone numbers (last two digits kept so support can still tell numbers apart).
/// </summary>
public sealed partial class RedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            var redacted = Redact(value);
            if (!ReferenceEquals(redacted, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, redacted));
            }
        }
    }

    /// <summary>Returns the same instance when nothing needed masking.</summary>
    private static LogEventPropertyValue Redact(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                var masked = Mask(text);
                return ReferenceEquals(masked, text) ? value : new ScalarValue(masked);
            case SequenceValue sequence:
                var items = sequence.Elements.Select(Redact).ToList();
                return items.Where((v, i) => !ReferenceEquals(v, sequence.Elements[i])).Any() ? new SequenceValue(items) : value;
            case StructureValue structure:
                var props = structure.Properties.Select(p => new LogEventProperty(p.Name, Redact(p.Value))).ToList();
                return props.Where((p, i) => !ReferenceEquals(p.Value, structure.Properties[i].Value)).Any() ? new StructureValue(props, structure.TypeTag) : value;
            default:
                return value;
        }
    }

    public static string Mask(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return text;
        }

        var result = ClientLink().Replace(text, "/d/[link]");
        result = SecretQuery().Replace(result, m => m.Groups[1].Value + "=[redacted]");
        result = Bearer().Replace(result, "Bearer [redacted]");
        result = Email().Replace(result, m => m.Value[0] + "***@" + m.Groups["domain"].Value);
        result = Phone().Replace(result, m =>
        {
            var digits = m.Value.Count(char.IsDigit);
            return digits is >= 9 and <= 15 ? "***" + m.Value[^2..] : m.Value;
        });
        return result == text ? text : result;
    }

    [GeneratedRegex(@"/d/[A-Za-z0-9_-]{16,}", RegexOptions.None, 200)]
    private static partial Regex ClientLink();

    [GeneratedRegex(@"\b(token|code|key|secret|password|hash)=[^&\s""]+", RegexOptions.IgnoreCase, 200)]
    private static partial Regex SecretQuery();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase, 200)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})\b", RegexOptions.None, 200)]
    private static partial Regex Email();

    // + and digits with spaces or dashes, e.g. +263 77 123 4567 or 0771234567. Not next to letters, dots or dashes, so GUIDs, dates and amounts are left alone.
    [GeneratedRegex(@"(?<![\w.-])\+?\d[\d \-]{7,18}\d(?![\w.-])", RegexOptions.None, 200)]
    private static partial Regex Phone();
}
