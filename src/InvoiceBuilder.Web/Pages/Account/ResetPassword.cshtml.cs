using System.ComponentModel.DataAnnotations;
using System.Text;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace InvoiceBuilder.Web.Pages.Account;

[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(InvoiceBuilder.Web.Security.AuthRateLimit.Policy)]
public sealed class ResetPasswordModel(UserManager<AppUser> users) : PageModel
{
    [BindProperty(SupportsGet = true), Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true), Required]
    public string Token { get; set; } = string.Empty;

    [BindProperty, Required]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Error = "Enter a new password.";
            return Page();
        }

        var user = await users.FindByEmailAsync(Email);
        if (user is null)
        {
            return RedirectToPage("/Account/SignIn");
        }

        string raw;
        try
        {
            raw = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Token));
        }
        catch (FormatException)
        {
            Error = "This reset link is not valid.";
            return Page();
        }

        var result = await users.ResetPasswordAsync(user, raw, Password);
        if (!result.Succeeded)
        {
            Error = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        return RedirectToPage("/Account/SignIn");
    }
}
