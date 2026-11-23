using System.Text;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>Builds <c>@font-face</c> rules with the Manrope and IBM Plex Mono woff2 files as data URIs, so PDF pages need no network.</summary>
public sealed class FontEmbedder
{
    private static readonly (string Family, string FilePrefix, int[] Weights)[] Faces =
    [
        ("Manrope", "manrope-latin", [400, 500, 600, 700, 800]),
        ("IBM Plex Mono", "ibm-plex-mono-latin", [400, 500, 600]),
    ];

    public FontEmbedder(string directory)
    {
        var css = new StringBuilder();
        foreach (var (family, prefix, weights) in Faces)
        {
            foreach (var weight in weights)
            {
                var path = Path.Combine(directory, $"{prefix}-{weight}-normal.woff2");
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException($"Font file not found: {path}");
                }

                css.Append("@font-face{font-family:'").Append(family).Append("';font-style:normal;font-weight:")
                    .Append(weight)
                    .Append(";src:url(data:font/woff2;base64,")
                    .Append(Convert.ToBase64String(File.ReadAllBytes(path)))
                    .Append(") format('woff2')}");
            }
        }

        FontFaceCss = css.ToString();
    }

    /// <summary>Copied next to the app from <c>assets/fonts</c> at build time.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Rendering", "Fonts");

    public string FontFaceCss { get; }
}
