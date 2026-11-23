using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace InvoiceBuilder.Application.Templates;

/// <summary>Picks up to three dominant colours from a PNG or SVG logo with simple 4-bit-per-channel quantisation (no imaging package).</summary>
public static partial class LogoPalette
{
    private const int MaxSamples = 200_000;

    public static IReadOnlyList<string> Extract(byte[] content, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(content);
        var counts = contentType?.Contains("svg", StringComparison.OrdinalIgnoreCase) == true
            ? FromSvg(content)
            : FromPng(content);
        return Top(counts);
    }

    private static Dictionary<int, long> FromSvg(byte[] content)
    {
        var counts = new Dictionary<int, long>();
        var text = System.Text.Encoding.UTF8.GetString(content);
        foreach (Match m in SvgHex().Matches(text))
        {
            var hex = m.Groups[1].Value;
            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            }

            var rgb = Convert.ToInt32(hex, 16);
            Add(counts, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 1);
        }

        return counts;
    }

    private static Dictionary<int, long> FromPng(byte[] data)
    {
        var counts = new Dictionary<int, long>();
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length < 33 || !data.AsSpan(0, 8).SequenceEqual(signature))
        {
            return counts;
        }

        int width = 0, height = 0, depth = 0, colourType = 0, interlace = 0;
        byte[] palette = [];
        using var idat = new MemoryStream();
        var pos = 8;
        while (pos + 8 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos, 4));
            var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            pos += 8;
            if (length < 0 || pos + length > data.Length)
            {
                return counts;
            }

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos + 4, 4));
                    depth = data[pos + 8];
                    colourType = data[pos + 9];
                    interlace = data[pos + 12];
                    break;
                case "PLTE":
                    palette = data.AsSpan(pos, length).ToArray();
                    break;
                case "IDAT":
                    idat.Write(data, pos, length);
                    break;
            }

            pos += length + 4;
            if (type == "IEND")
            {
                break;
            }
        }

        var channels = colourType switch { 2 => 3, 6 => 4, 3 => 1, _ => 0 };
        if (width <= 0 || height <= 0 || depth != 8 || interlace != 0 || channels == 0 || (long)width * height > 25_000_000)
        {
            return counts;
        }

        var stride = width * channels;
        byte[] raw;
        try
        {
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            using var output = new MemoryStream();
            z.CopyTo(output);
            raw = output.ToArray();
        }
        catch (InvalidDataException)
        {
            return counts;
        }

        if (raw.Length < (long)(stride + 1) * height)
        {
            return counts;
        }

        var prev = new byte[stride];
        var cur = new byte[stride];
        var step = Math.Max(1, (int)Math.Sqrt((double)width * height / MaxSamples));
        for (var y = 0; y < height; y++)
        {
            var offset = y * (stride + 1);
            var filter = raw[offset];
            Array.Copy(raw, offset + 1, cur, 0, stride);
            Unfilter(filter, cur, prev, channels);
            if (y % step == 0)
            {
                for (var x = 0; x < width; x += step)
                {
                    var i = x * channels;
                    int r, g, b, a = 255;
                    if (colourType == 3)
                    {
                        var p = cur[i] * 3;
                        if (p + 2 >= palette.Length)
                        {
                            continue;
                        }

                        (r, g, b) = (palette[p], palette[p + 1], palette[p + 2]);
                    }
                    else
                    {
                        (r, g, b) = (cur[i], cur[i + 1], cur[i + 2]);
                        if (colourType == 6)
                        {
                            a = cur[i + 3];
                        }
                    }

                    if (a >= 128)
                    {
                        Add(counts, r, g, b, 1);
                    }
                }
            }

            (prev, cur) = (cur, prev);
        }

        return counts;
    }

    private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
    {
        for (var i = 0; i < cur.Length; i++)
        {
            var a = i >= bpp ? cur[i - bpp] : 0;
            var b = prev[i];
            var c = i >= bpp ? prev[i - bpp] : 0;
            cur[i] = (byte)(cur[i] + filter switch
            {
                1 => a,
                2 => b,
                3 => (a + b) / 2,
                4 => Paeth(a, b, c),
                _ => 0,
            });
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Add(Dictionary<int, long> counts, int r, int g, int b, long weight)
    {
        if (r > 240 && g > 240 && b > 240)
        {
            return;
        }

        var key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
        counts[key] = counts.GetValueOrDefault(key) + weight;
    }

    private static List<string> Top(Dictionary<int, long> counts)
    {
        var chosen = new List<int>();
        foreach (var key in counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key))
        {
            if (chosen.All(c => Distance(c, key) >= 3))
            {
                chosen.Add(key);
            }

            if (chosen.Count == 3)
            {
                break;
            }
        }

        return chosen.Select(k => $"#{((k >> 8) & 0xF) * 17:X2}{((k >> 4) & 0xF) * 17:X2}{(k & 0xF) * 17:X2}").ToList();
    }

    private static int Distance(int a, int b) =>
        Math.Abs(((a >> 8) & 0xF) - ((b >> 8) & 0xF)) + Math.Abs(((a >> 4) & 0xF) - ((b >> 4) & 0xF)) + Math.Abs((a & 0xF) - (b & 0xF));

    [GeneratedRegex("#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\\b")]
    private static partial Regex SvgHex();
}
