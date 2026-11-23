using System.Security.Claims;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Web.Security;

/// <summary>Adds tenant, business assignments, selected business and 2FA state to the signed-in principal.</summary>
public sealed class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db) : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, options)
{
    public const string SelectedBusinessClaim = "ib:selected";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.TenantId, user.TenantId.ToString()));

        // Reviewed admin query: runs at sign-in, before any tenant scope exists.
        var businessIds = await db.UserBusinesses.IgnoreQueryFilters()
            .Where(u => u.UserId == user.Id)
            .Select(u => u.BusinessId)
            .ToListAsync();
        foreach (var businessId in businessIds)
        {
            identity.AddClaim(new Claim(AppClaims.BusinessId, businessId.ToString()));
        }

        var isOwner = identity.HasClaim(identity.RoleClaimType, Roles.Owner);
        var selected = SelectFor(isOwner, user.SelectedBusinessId, businessIds);
        if (selected is { } selectedId)
        {
            identity.AddClaim(new Claim(SelectedBusinessClaim, selectedId.ToString()));
        }

        if (user.TwoFactorEnabled)
        {
            identity.AddClaim(new Claim(AppClaims.TwoFactorEnrolled, "true"));
        }

        return identity;
    }

    /// <summary>
    /// The business a user works in (R2). Owners may pick any business or none (all businesses). Staff only ever get an
    /// assigned business; staff with no assignment get <see cref="Guid.Empty"/>, which matches no data, because a
    /// missing selection would otherwise widen the query filter to the whole tenant.
    /// </summary>
    public static Guid? SelectFor(bool isOwner, Guid? preferred, IReadOnlyList<Guid> assigned)
    {
        ArgumentNullException.ThrowIfNull(assigned);
        if (isOwner)
        {
            return preferred;
        }

        if (preferred is { } p && assigned.Contains(p))
        {
            return p;
        }

        return assigned.Count > 0 ? assigned[0] : Guid.Empty;
    }
}

/// <summary>Signed-in user tenant, business and identity for the request. A webhook override wins when set.</summary>
public sealed class WebCurrentScope(IHttpContextAccessor accessor, RequestScope requestScope, CircuitUserAccessor circuit) : ICurrentTenant, ICurrentBusiness, ICurrentUser
{
    private ClaimsPrincipal? User => circuit.User ?? accessor.HttpContext?.User;

    public Guid TenantId =>
        requestScope.OverrideTenantId
        ?? (Guid.TryParse(User?.FindFirstValue(AppClaims.TenantId), out var tenant) ? tenant : Guid.Empty);

    public Guid? BusinessId => requestScope.BusinessId ?? BusinessFromClaims(User);

    /// <summary>
    /// Defence in depth for R2: a Staff principal is always scoped to one of its assigned businesses, even if the
    /// cookie was issued before this rule existed. <see cref="Guid.Empty"/> means "no access" and matches no rows.
    /// </summary>
    public static Guid? BusinessFromClaims(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        Guid? selected = Guid.TryParse(user.FindFirstValue(AppClaimsPrincipalFactory.SelectedBusinessClaim), out var business) ? business : null;
        if (user.IsInRole(Roles.Owner))
        {
            return selected;
        }

        var assigned = user.FindAll(AppClaims.BusinessId)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
        return AppClaimsPrincipalFactory.SelectFor(false, selected, assigned);
    }

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string Label => User?.Identity?.Name ?? "System";
}
