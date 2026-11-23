using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public sealed record LogoThemeSuggestion(string Ink, string Accent, string Muted);

public sealed record SuggestThemeFromLogoQuery : IRequest<Result<LogoThemeSuggestion>>;

public sealed class SuggestThemeFromLogoHandler(ICurrentBusiness currentBusiness, IBusinessRepository businesses)
    : IRequestHandler<SuggestThemeFromLogoQuery, Result<LogoThemeSuggestion>>
{
    public async Task<Result<LogoThemeSuggestion>> Handle(SuggestThemeFromLogoQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<LogoThemeSuggestion>(TemplateErrors.NoBusiness);
        }

        var business = await businesses.GetByIdAsync(businessId, cancellationToken);
        if (business?.Logo is not { Length: > 0 } logo)
        {
            return Result.Failure<LogoThemeSuggestion>(new Error("Theme.NoLogo", "Upload a logo in business settings first."));
        }

        var colours = LogoPalette.Extract(logo, business.LogoContentType);
        if (colours.Count == 0)
        {
            return Result.Failure<LogoThemeSuggestion>(new Error("Theme.NoColours", "No colours could be read from the logo. PNG (8-bit, not interlaced) and SVG logos are supported."));
        }

        var accent = colours[0];
        var ink = colours.OrderBy(Luma).First();
        var muted = colours.Count > 2 ? colours[2] : "#6B6B6B";
        return Result.Success(new LogoThemeSuggestion(ink == accent && colours.Count == 1 ? "#111111" : ink, accent, muted));
    }

    private static int Luma(string hex)
    {
        var v = Convert.ToInt32(hex[1..], 16);
        return (((v >> 16) & 0xFF) * 299) + (((v >> 8) & 0xFF) * 587) + ((v & 0xFF) * 114);
    }
}
