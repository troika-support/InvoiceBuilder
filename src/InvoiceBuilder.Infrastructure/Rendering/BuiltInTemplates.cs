using System.Reflection;
using InvoiceBuilder.Application.Templates;

namespace InvoiceBuilder.Infrastructure.Rendering;

public sealed class BuiltInTemplates : IBuiltInTemplates
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Minimal"] = "minimal.json",
        ["Bold header"] = "bold-header.json",
        ["Classic ledger"] = "classic-ledger.json",
    };

    public IReadOnlyList<string> Names { get; } = [.. Files.Keys];

    public string? Load(string name)
    {
        if (!Files.TryGetValue(name, out var file))
        {
            return null;
        }

        var assembly = typeof(BuiltInTemplates).Assembly;
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("BuiltIns." + file, StringComparison.Ordinal));
        if (resource is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
