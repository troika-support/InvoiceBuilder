using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>
/// The one place theme colours and fonts are resolved. Output is always a validated hex colour or a sanitised font name,
/// so user-controlled values can never reach a <c>style</c> attribute unchecked.
/// </summary>
public sealed partial class ThemeTokenResolver(IReadOnlyDictionary<string, string> tokens, ILogger logger)
{
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
    {
        ["ink"] = "#111111",
        ["paper"] = "#FFFFFF",
        ["accent"] = "#F59E0B",
        ["muted"] = "#6B6B6B",
    };

    public static bool IsHex(string? value) => value is not null && HexPattern().IsMatch(value);

    /// <summary>Token name or hex. Unknown or invalid values fall back to <paramref name="fallbackToken"/> (ink or paper) and log a warning.</summary>
    public string Color(string? value, string fallbackToken = "ink")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Token(fallbackToken);
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "transparent", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (trimmed[0] == '#')
        {
            if (IsHex(trimmed))
            {
                return trimmed;
            }

            LogInvalidColour(trimmed, fallbackToken);
            return Token(fallbackToken);
        }

        if (tokens.TryGetValue(trimmed, out var resolved) && IsHex(resolved))
        {
            return resolved;
        }

        LogUnknownToken(trimmed, fallbackToken);
        return Token(fallbackToken);
    }

    /// <summary>"body" and "mono" come from the theme's fonts; anything else must be a plain family name.</summary>
    public string FontFamily(string? value)
    {
        var key = string.IsNullOrWhiteSpace(value) ? "body" : value.Trim();
        return key switch
        {
            "body" => ThemeFont("fonts.body", "Manrope"),
            "mono" => ThemeFont("fonts.mono", "IBM Plex Mono"),
            _ when SafeFamily().IsMatch(key) => key,
            _ => ThemeFont("fonts.body", "Manrope"),
        };
    }

    private string Token(string name) =>
        tokens.TryGetValue(name, out var value) && IsHex(value)
            ? value
            : Defaults.GetValueOrDefault(name, Defaults["ink"]);

    private string ThemeFont(string key, string fallback) =>
        tokens.TryGetValue(key, out var family) && SafeFamily().IsMatch(family) ? family : fallback;

    [GeneratedRegex("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex HexPattern();

    [GeneratedRegex("^[A-Za-z0-9 ]{1,40}$")]
    private static partial Regex SafeFamily();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Theme token {Token} not found; using {Fallback}")]
    private partial void LogUnknownToken(string token, string fallback);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Colour {Colour} is not a valid hex value; using {Fallback}")]
    private partial void LogInvalidColour(string colour, string fallback);
}
