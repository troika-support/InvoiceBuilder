using System.Text.Json;
using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Application.Templates;

/// <summary>Designer output, schema v1 (template-rendering.md §2). All positions and sizes are millimetres.</summary>
public sealed record TemplateDefinition
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public PageDefinition Page { get; init; } = new();

    public IReadOnlyList<string> ThemeTokens { get; init; } = [];

    public PageLayer FirstPage { get; init; } = new();

    public ContinuationLayer ContinuationPage { get; init; } = new();

    public FlowDefinition Flow { get; init; } = new();
}

public sealed record PageDefinition
{
    public string Size { get; init; } = "A4";

    public double WidthMm { get; init; } = 210;

    public double HeightMm { get; init; } = 297;
}

public sealed record PageLayer
{
    public IReadOnlyList<TemplateElement> Elements { get; init; } = [];
}

public sealed record ContinuationLayer
{
    public IReadOnlyList<TemplateElement> Elements { get; init; } = [];

    /// <summary>Where the items table resumes on page 2 and later (height of the thead spacer).</summary>
    public double ItemsTop { get; init; } = 40;
}

public sealed record FlowDefinition
{
    public ItemsTableElement ItemsTable { get; init; } = new();

    public TotalsFlow Totals { get; init; } = new();

    public InstalmentScheduleFlow? InstalmentSchedule { get; init; }

    /// <summary>Space kept clear at the bottom of every page.</summary>
    public double FooterReserveMm { get; init; } = 30;
}

public sealed record TotalsFlow
{
    public string Mode { get; init; } = "followTable";

    public double GapMm { get; init; } = 12;

    public double XMm { get; init; } = 108;

    public double WidthMm { get; init; } = 84;

    public double HeightMm { get; init; } = 20;

    public TextStyle Style { get; init; } = new();
}

public sealed record InstalmentScheduleFlow
{
    public double GapMm { get; init; } = 8;

    public double XMm { get; init; } = 18;

    public double WidthMm { get; init; } = 174;
}

public static class ElementTypes
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Shape = "shape";
    public const string Line = "line";
    public const string Field = "field";
    public const string PayQr = "payQr";
    public const string Signature = "signature";
}

public static class RepeatModes
{
    public const string First = "first";
    public const string All = "all";
    public const string Last = "last";
}

/// <summary>One designer element. Fields apply by <see cref="Type"/>; unused ones stay at their defaults.</summary>
public sealed record TemplateElement
{
    public string Id { get; init; } = string.Empty;

    public string Type { get; init; } = ElementTypes.Text;

    public double XMm { get; init; }

    public double YMm { get; init; }

    public double WidthMm { get; init; }

    public double HeightMm { get; init; }

    public int Rotation { get; init; }

    public bool Locked { get; init; }

    public bool Hidden { get; init; }

    public int ZIndex { get; init; }

    public string Repeat { get; init; } = RepeatModes.First;

    // text, field, signature
    public string? Text { get; init; }

    public string? Binding { get; init; }

    public string? Format { get; init; }

    public string? Font { get; init; }

    public double SizePt { get; init; } = 10;

    public int Weight { get; init; } = 400;

    public string? Color { get; init; }

    public string Align { get; init; } = "left";

    public double LineHeight { get; init; } = 1.3;

    public double LetterSpacing { get; init; }

    public bool Uppercase { get; init; }

    // shape
    public string Shape { get; init; } = "rect";

    public string? Fill { get; init; }

    public string? Stroke { get; init; }

    public double StrokeWidthMm { get; init; }

    public double RadiusMm { get; init; }

    // line
    public double X2Mm { get; init; }

    public double Y2Mm { get; init; }

    public string Dash { get; init; } = "solid";

    public double Opacity { get; init; } = 1;

    // image
    public string? AssetId { get; init; }

    public string Fit { get; init; } = "contain";
}

public sealed record TextStyle
{
    public string? Font { get; init; }

    public double SizePt { get; init; } = 9;

    public int Weight { get; init; } = 400;

    public string? Color { get; init; }

    public string? Fill { get; init; }

    public string? BorderColor { get; init; }

    public bool Uppercase { get; init; }

    public double LetterSpacing { get; init; }
}

public sealed record ItemsColumns
{
    public double Description { get; init; } = 46;

    public double Qty { get; init; } = 8;

    public double Rate { get; init; } = 16;

    public double Tax { get; init; } = 8;

    public double Amount { get; init; } = 22;
}

public sealed record ItemsTableElement
{
    public double XMm { get; init; } = 18;

    public double YMm { get; init; } = 90;

    public double WidthMm { get; init; } = 174;

    public ItemsColumns Columns { get; init; } = new();

    public TextStyle Header { get; init; } = new() { Weight = 700, Uppercase = true, LetterSpacing = 0.06 };

    public TextStyle Row { get; init; } = new();

    public double RowHeightMm { get; init; } = 7.5;

    public double HeaderHeightMm { get; init; } = 8;

    public bool Zebra { get; init; }

    public bool ShowDetailLine { get; init; }
}

public static class TemplateDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Result<TemplateDefinition> Parse(string json)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<TemplateDefinition>(json, Options);
            if (definition is null)
            {
                return Result.Failure<TemplateDefinition>(new Error("Template.Invalid", "The template is empty."));
            }

            return definition.SchemaVersion > TemplateDefinition.CurrentSchemaVersion
                ? Result.Failure<TemplateDefinition>(new Error("Template.SchemaTooNew", "This template was made with a newer version of the designer."))
                : Result.Success(definition);
        }
        catch (JsonException)
        {
            return Result.Failure<TemplateDefinition>(new Error("Template.Invalid", "The template could not be read."));
        }
    }

    public static string Serialize(TemplateDefinition definition) => JsonSerializer.Serialize(definition, Options);
}
