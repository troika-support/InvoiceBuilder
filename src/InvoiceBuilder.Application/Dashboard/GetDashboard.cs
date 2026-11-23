using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Application.Dashboard;

/// <summary>What the dashboard header and body need. <see cref="FirstName"/> is null when only an email is known.</summary>
public sealed record DashboardView(DashboardFigures Figures, string? FirstName);

/// <summary>Dashboard for the selected business in one currency (R12). <paramref name="Refresh"/> skips the 60 s cache.</summary>
public sealed record GetDashboardQuery(string Currency, bool Refresh = false) : IRequest<Result<DashboardView>>;

public sealed class GetDashboardHandler(
    ICurrentBusiness currentBusiness,
    ICurrentUser currentUser,
    IUserPreferenceStore users,
    IDashboardQueries queries,
    IDashboardCache cache,
    IDisplayFormatter format,
    IOptions<AppOptions> app,
    TimeProvider timeProvider) : IRequestHandler<GetDashboardQuery, Result<DashboardView>>
{
    public async Task<Result<DashboardView>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<DashboardView>(new Error("Dashboard.NoBusiness", "Pick a business first."));
        }

        var currency = string.Equals(request.Currency, "ZWG", StringComparison.OrdinalIgnoreCase) ? "ZWG" : "USD";
        if (request.Refresh)
        {
            cache.Invalidate(businessId);
        }

        var figures = await cache.GetOrCreateAsync(businessId, currency, async ct =>
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(app.Value.TimeZone);
            var now = timeProvider.GetUtcNow();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            var source = await queries.LoadAsync(DashboardCalculator.ChartStart(now, zone), today.AddDays(DashboardCalculator.DueSoonDays), ct);
            return DashboardCalculator.Calculate(source, currency, now, zone, format);
        }, cancellationToken);

        var name = currentUser.UserId is { } userId ? await users.GetDisplayNameAsync(userId, cancellationToken) : null;
        return Result.Success(new DashboardView(figures, FirstName(name)));
    }

    /// <summary>First word of the display name; null if the name is empty or is only an email address.</summary>
    public static string? FirstName(string? displayName)
    {
        var first = displayName?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) || first.Contains('@', StringComparison.Ordinal) ? null : first;
    }
}
