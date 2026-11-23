using System.Text.RegularExpressions;
using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Application.Templates;

/// <summary>The binding whitelist from template-rendering.md §3. Nothing else resolves.</summary>
public static class TemplateBindings
{
    public static IReadOnlySet<string> Allowed { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "business.tradingName", "business.registeredName", "business.email", "business.phone", "business.address",
        "business.logo", "business.bankDetails", "document.status",
        "client.name", "client.email", "client.phone", "client.address",
        "document.typeLabel", "document.number", "document.issueDate", "document.dueDate", "document.validUntil", "document.currency",
        "document.subtotal", "document.discountTotal", "document.taxTotal", "document.total", "document.amountPaid", "document.balance",
        "document.notes", "document.terms", "document.clientLinkUrl",
        "plan.count", "plan.nextInstalment.number", "plan.nextInstalment.amount", "plan.nextInstalment.dueDate", "plan.overdueAmount",
        "acceptance.signerName", "acceptance.acceptedAt",
        "page.number", "page.count",
    };
}

/// <summary>Server-side check of a designer save (prompt 16): unknown bindings, unknown element types, and elements off the page.</summary>
public static partial class TemplateDefinitionValidator
{
    private const double Tolerance = 0.05;

    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        ElementTypes.Text, ElementTypes.Image, ElementTypes.Shape, ElementTypes.Line, ElementTypes.Field, ElementTypes.PayQr, ElementTypes.Signature,
    };

    public static Result Validate(TemplateDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var width = definition.Page.WidthMm;
        var height = definition.Page.HeightMm;
        if (width is <= 0 or > 1000 || height is <= 0 or > 1500)
        {
            return Fail("Template.PageSize", "The page size is not valid.");
        }

        foreach (var element in definition.FirstPage.Elements.Concat(definition.ContinuationPage.Elements))
        {
            var result = ValidateElement(element, width, height);
            if (result.IsFailure)
            {
                return result;
            }
        }

        var table = definition.Flow.ItemsTable;
        if (!Inside(table.XMm, table.YMm, table.WidthMm, 1, width, height))
        {
            return Fail("Template.OutsidePage", "The items table is outside the page.");
        }

        var totals = definition.Flow.Totals;
        if (totals.XMm < -Tolerance || totals.XMm + totals.WidthMm > width + Tolerance)
        {
            return Fail("Template.OutsidePage", "The totals block is outside the page.");
        }

        if (definition.Flow.InstalmentSchedule is { } schedule && (schedule.XMm < -Tolerance || schedule.XMm + schedule.WidthMm > width + Tolerance))
        {
            return Fail("Template.OutsidePage", "The instalment schedule is outside the page.");
        }

        return Result.Success();
    }

    private static Result ValidateElement(TemplateElement e, double width, double height)
    {
        if (!KnownTypes.Contains(e.Type))
        {
            return Fail("Template.UnknownElement", "The template has an element the designer does not know.");
        }

        if (e.Binding is not null && !TemplateBindings.Allowed.Contains(e.Binding))
        {
            return Fail("Template.UnknownBinding", $"“{e.Binding}” is not a field you can bind to.");
        }

        if (e.Text is not null)
        {
            foreach (Match marker in BindingMarker().Matches(e.Text))
            {
                var path = marker.Groups[1].Value;
                if (!TemplateBindings.Allowed.Contains(path))
                {
                    return Fail("Template.UnknownBinding", $"“{path}” is not a field you can bind to.");
                }
            }
        }

        var inside = e.Type == ElementTypes.Line
            ? Inside(e.XMm, e.YMm, 0, 0, width, height) && Inside(e.X2Mm, e.Y2Mm, 0, 0, width, height)
            : InsideRotated(e, width, height);
        return inside
            ? Result.Success()
            : Fail("Template.OutsidePage", e.Type == ElementTypes.Line
                ? FormattableString.Invariant($"Line “{e.Id}” is outside the page (from {e.XMm:0.##}, {e.YMm:0.##} to {e.X2Mm:0.##}, {e.Y2Mm:0.##} mm; page {width:0.##} × {height:0.##} mm).")
                : FormattableString.Invariant($"Element “{e.Id}” is outside the page (x {e.XMm:0.##}, y {e.YMm:0.##}, width {e.WidthMm:0.##}, height {e.HeightMm:0.##} mm; page {width:0.##} × {height:0.##} mm)."));
    }

    private static bool InsideRotated(TemplateElement e, double pageWidth, double pageHeight)
    {
        if (e.WidthMm < 0 || e.HeightMm < 0)
        {
            return false;
        }

        var radians = e.Rotation * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var boundsWidth = (e.WidthMm * cos) + (e.HeightMm * sin);
        var boundsHeight = (e.WidthMm * sin) + (e.HeightMm * cos);
        var x = e.XMm + (e.WidthMm / 2) - (boundsWidth / 2);
        var y = e.YMm + (e.HeightMm / 2) - (boundsHeight / 2);
        return Inside(x, y, boundsWidth, boundsHeight, pageWidth, pageHeight);
    }

    private static bool Inside(double x, double y, double w, double h, double pageWidth, double pageHeight) =>
        x >= -Tolerance && y >= -Tolerance && w >= 0 && h >= 0 && x + w <= pageWidth + Tolerance && y + h <= pageHeight + Tolerance;

    private static Result Fail(string code, string message) => Result.Failure(new Error(code, message));

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}")]
    private static partial Regex BindingMarker();
}
