using MudBlazor;

namespace InvoiceBuilder.Web.Theme;

/// <summary>
/// MudBlazor theme for the Invoice Builder app chrome (docs/design-system.md §2–§4).
/// Written against MudBlazor 8.x (FontWeight/FontSize are strings). If the installed
/// version differs, keep the values and adjust the property shapes.
/// </summary>
public static class IbTheme
{
    private static readonly string[] Sans = ["Manrope", "system-ui", "-apple-system", "Segoe UI", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#115E59",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#334155",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#14B8A6",
            Info = "#1E3A8A",
            Success = "#166534",
            Warning = "#78350F",
            Error = "#991B1B",
            Background = "#F4F5F7",
            BackgroundGray = "#F1F5F9",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A",
            DrawerBackground = "#0F172A",
            DrawerText = "#CBD5E1",
            DrawerIcon = "#CBD5E1",
            TextPrimary = "#0F172A",
            TextSecondary = "#434E60",
            ActionDefault = "#334155",
            LinesDefault = "#E2E6EB",
            LinesInputs = "#D5DAE1",
            TableLines = "#EEF0F3",
            Divider = "#E2E6EB",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2DD4BF",
            PrimaryContrastText = "#042F2E",
            Secondary = "#CBD5E1",
            SecondaryContrastText = "#0B1120",
            Tertiary = "#14B8A6",
            Info = "#DBEAFE",
            Success = "#86EFAC",
            Warning = "#FEF3C7",
            Error = "#FCA5A5",
            Background = "#0B1120",
            BackgroundGray = "#1F2937",
            Surface = "#111827",
            AppbarBackground = "#111827",
            AppbarText = "#F1F5F9",
            DrawerBackground = "#020617",
            DrawerText = "#CBD5E1",
            DrawerIcon = "#CBD5E1",
            TextPrimary = "#F1F5F9",
            TextSecondary = "#A3B0C2",
            ActionDefault = "#CBD5E1",
            LinesDefault = "#1F2937",
            LinesInputs = "#334155",
            TableLines = "#1F2937",
            Divider = "#1F2937",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Sans, FontSize = "14px", LineHeight = "20px", FontWeight = "400" },
            H1 = new H1Typography { FontFamily = Sans, FontSize = "26px", LineHeight = "32px", FontWeight = "800", LetterSpacing = "-0.02em" },
            H2 = new H2Typography { FontFamily = Sans, FontSize = "18px", LineHeight = "24px", FontWeight = "800" },
            H3 = new H3Typography { FontFamily = Sans, FontSize = "16px", LineHeight = "22px", FontWeight = "700" },
            Subtitle1 = new Subtitle1Typography { FontFamily = Sans, FontSize = "14px", LineHeight = "20px", FontWeight = "600" },
            Body1 = new Body1Typography { FontFamily = Sans, FontSize = "14px", LineHeight = "20px", FontWeight = "400" },
            Body2 = new Body2Typography { FontFamily = Sans, FontSize = "13px", LineHeight = "18px", FontWeight = "500" },
            Caption = new CaptionTypography { FontFamily = Sans, FontSize = "12px", LineHeight = "16px", FontWeight = "600" },
            Button = new ButtonTypography { FontFamily = Sans, FontSize = "14px", LineHeight = "20px", FontWeight = "700", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "240px",
            DrawerMiniWidthLeft = "72px",
        },
    };
}
