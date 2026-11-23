using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>Reissues the auth cookie so a business switch saved by the shell takes effect (spec §5.1).</summary>
[Authorize]
[Route("account/refresh")]
public sealed class SessionRefreshController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Refresh([FromQuery] string? returnUrl)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await signInManager.RefreshSignInAsync(user);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
