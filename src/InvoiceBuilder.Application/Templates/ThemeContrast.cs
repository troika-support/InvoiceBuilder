using System.Globalization;

namespace InvoiceBuilder.Application.Templates;

/// <summary>WCAG contrast ratios for theme token pairs (design-system §2.3): warn below 4.5:1, block publish below 3:1.</summary>
public static class ThemeContrast
{
    public const double WarnBelow = 4.5;
    public const double BlockBelow = 3.0;

    public static readonly IReadOnlyList<(string Text, string Background)> Pairs =
    [
        ("ink", "paper"),
        ("muted", "paper"),
        ("ink", "accent"),
        ("clientMuted", "clientSurface"),
    ];

    public static double? Ratio(string? foreground, string? background)
    {
        if (!TryLuminance(foreground, out var a) || !TryLuminance(background, out var b))
        {
            return null;
        }

        var (hi, lo) = a >= b ? (a, b) : (b, a);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static IReadOnlyList<ContrastResult> Check(IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var results = new List<ContrastResult>();
        foreach (var (text, background) in Pairs)
        {
            if (tokens.TryGetValue(text, out var fg) && tokens.TryGetValue(background, out var bg) && Ratio(fg, bg) is { } ratio)
            {
                results.Add(new ContrastResult(text, background, Math.Round(ratio, 2), ratio < BlockBelow ? ContrastLevel.Fail : ratio < WarnBelow ? ContrastLevel.Warn : ContrastLevel.Pass));
            }
        }

        return results;
    }

    private static bool TryLuminance(string? hex, out double luminance)
    {
        luminance = 0;
        if (hex is not { Length: 7 } || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        double Channel(int v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        luminance = (0.2126 * Channel((rgb >> 16) & 0xFF)) + (0.7152 * Channel((rgb >> 8) & 0xFF)) + (0.0722 * Channel(rgb & 0xFF));
        return true;
    }
}

public enum ContrastLevel
{
    Pass,
    Warn,
    Fail,
}

public sealed record ContrastResult(string Text, string Background, double Ratio, ContrastLevel Level);
