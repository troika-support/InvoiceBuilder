using System.ComponentModel.DataAnnotations;
using System.Text;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace InvoiceBuilder.Web.Pages.Account;

[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(InvoiceBuilder.Web.Security.AuthRateLimit.Policy)]
public sealed class ForgotPasswordModel(UserManager<AppUser> users, IEmailSender email, Microsoft.Extensions.Options.IOptions<InvoiceBuilder.Application.Options.AppOptions> app) : PageModel
{
    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await users.FindByEmailAsync(Email);
        if (user is not null)
        {
            var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
            // Built from the configured public URL, never the request's Host header, so a forged Host can't redirect the link.
            var path = Url.Page("/Account/ResetPassword", null, new { email = Email, token })!;
            var baseUrl = string.IsNullOrWhiteSpace(app.Value.PublicBaseUrl) ? $"{Request.Scheme}://{Request.Host}" : app.Value.PublicBaseUrl.TrimEnd('/');
            var link = baseUrl + path;
            await email.SendAsync(Email, "Reset your password", $"Reset your password: {link}", cancellationToken);
        }

        Sent = true;
        return Page();
    }
}
