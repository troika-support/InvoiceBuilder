using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Templates;
using MediatR;

namespace InvoiceBuilder.Application.Templates;

public sealed record ThemeItem(Guid Id, string Name, IReadOnlyDictionary<string, string> Tokens, IReadOnlyList<ContrastResult> Contrast, int UsedByTemplates, bool IsDefault, bool IsArchived = false);

public sealed record ListThemesQuery(bool IncludeArchived = false) : IRequest<Result<IReadOnlyList<ThemeItem>>>;

public sealed class ListThemesHandler(ITemplateRepository templates, IBusinessRepository businesses, ICurrentBusiness currentBusiness) : IRequestHandler<ListThemesQuery, Result<IReadOnlyList<ThemeItem>>>
{
    public async Task<Result<IReadOnlyList<ThemeItem>>> Handle(ListThemesQuery request, CancellationToken cancellationToken)
    {
        var themes = await templates.ListThemesAsync(cancellationToken);
        var all = await templates.ListAsync(cancellationToken);
        var usage = all.GroupBy(t => t.ThemeId).ToDictionary(g => g.Key, g => g.Count());
        var business = currentBusiness.BusinessId is { } businessId ? await businesses.GetByIdAsync(businessId, cancellationToken) : null;
        IReadOnlyList<ThemeItem> items = themes
            .Where(t => request.IncludeArchived || !t.IsArchived)
            .Select(t =>
            {
                var tokens = DocumentTokens.Parse(t.Tokens);
                var contrast = ThemeContrast.Check(tokens);
                AddFonts(tokens, t.Tokens);
                return new ThemeItem(t.Id, t.Name, tokens, contrast, usage.GetValueOrDefault(t.Id), business?.DefaultThemeId == t.Id, t.IsArchived);
            })
            .ToList();
        return Result.Success(items);
    }

    private static void AddFonts(Dictionary<string, string> tokens, string? json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json ?? "{}");
            if (doc.RootElement.TryGetProperty("fonts", out var fonts) && fonts.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in fonts.EnumerateObject().Where(p => p.Value.ValueKind == System.Text.Json.JsonValueKind.String))
                {
                    tokens[$"fonts.{p.Name}"] = p.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }
    }
}

/// <summary>Creates a theme (ThemeId null) or updates one. Tokens are validated hex colours and plain font names.</summary>
public sealed record SaveThemeCommand(
    Guid? ThemeId,
    string Name,
    string Ink,
    string Accent,
    string Paper,
    string Muted,
    string HeadingFont,
    string BodyFont) : ITransactionalCommand<Result<Guid>>;

public sealed partial class SaveThemeHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ITemplateRepository templates,
    IActivityLog activity,
    TimeProvider time) : IRequestHandler<SaveThemeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveThemeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(TemplateErrors.NoBusiness);
        }

        foreach (var colour in new[] { request.Ink, request.Accent, request.Paper, request.Muted })
        {
            if (!HexColour().IsMatch(colour))
            {
                return Result.Failure<Guid>(new Error("Theme.BadColour", "Colours must look like #RRGGBB."));
            }
        }

        foreach (var font in new[] { request.HeadingFont, request.BodyFont })
        {
            if (!SafeFont().IsMatch(font))
            {
                return Result.Failure<Guid>(new Error("Theme.BadFont", "Font names may only use letters, numbers, spaces and hyphens."));
            }
        }

        var existing = request.ThemeId is { } id ? await templates.GetThemeAsync(id, cancellationToken) : null;
        var previous = DocumentTokens.Parse(existing?.Tokens);
        var tokens = new Dictionary<string, object>
        {
            ["ink"] = request.Ink,
            ["accent"] = request.Accent,
            ["paper"] = request.Paper,
            ["muted"] = request.Muted,
            ["clientMuted"] = previous.GetValueOrDefault("clientMuted", "#44403C"),
            ["clientSurface"] = previous.GetValueOrDefault("clientSurface", "#F5F5F4"),
            ["fonts"] = new Dictionary<string, string> { ["heading"] = request.HeadingFont, ["body"] = request.BodyFont, ["mono"] = "IBM Plex Mono" },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(tokens);
        var now = time.GetUtcNow();

        if (existing is not null)
        {
            if (existing.BusinessId != businessId)
            {
                return Result.Failure<Guid>(TemplateErrors.NotFound);
            }

            var updated = existing.Update(request.Name, json, now);
            if (updated.IsFailure)
            {
                return Result.Failure<Guid>(updated.Error);
            }

            await activity.AddAsync(nameof(Theme), existing.Id, "ThemeUpdated", $"Theme {existing.Name} updated", cancellationToken);
            return Result.Success(existing.Id);
        }

        var created = Theme.Create(tenant.TenantId, businessId, request.Name, json, now);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        templates.AddTheme(created.Value);
        await activity.AddAsync(nameof(Theme), created.Value.Id, "ThemeCreated", $"Theme {created.Value.Name} created", cancellationToken);
        return Result.Success(created.Value.Id);
    }

    [System.Text.RegularExpressions.GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial System.Text.RegularExpressions.Regex HexColour();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9 \-]{1,60}$")]
    private static partial System.Text.RegularExpressions.Regex SafeFont();
}
