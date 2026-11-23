using System.Security.Claims;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Web.Security;

/// <summary>
/// Re-checks the security stamp of the user behind a Blazor circuit every few minutes. Without it an open circuit keeps
/// the claims it started with, so deactivating (which changes the stamp) a staff member or changing their businesses would not take effect until
/// they reloaded the page.
/// </summary>
public sealed class RevalidatingIdentityStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(2);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var principal = authenticationState.User;
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }

        if (!users.SupportsUserSecurityStamp)
        {
            return true;
        }

        var stampInClaims = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        return string.Equals(stampInClaims, await users.GetSecurityStampAsync(user), StringComparison.Ordinal);
    }
}
