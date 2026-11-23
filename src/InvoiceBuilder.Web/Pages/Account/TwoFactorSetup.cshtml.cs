using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace InvoiceBuilder.Web.Pages.Account;

[Authorize]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(InvoiceBuilder.Web.Security.AuthRateLimit.Policy)]
public sealed class TwoFactorSetupModel(UserManager<AppUser> users, SignInManager<AppUser> signIn, UrlEncoder encoder) : PageModel
{
    public string SharedKey { get; private set; } = string.Empty;

    public string QrCodeBase64 { get; private set; } = string.Empty;

    public IReadOnlyList<string>? RecoveryCodes { get; private set; }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/SignIn");
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/SignIn");
        }

        var code = Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            Error = "That code did not match. Check the code in your authenticator app and try again.";
            await LoadAsync(user);
            return Page();
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        RecoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList();
        await signIn.RefreshSignInAsync(user);
        return Page();
    }

    private async Task LoadAsync(AppUser user)
    {
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(key!);
        var uri = string.Format(CultureInfo.InvariantCulture, "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6", encoder.Encode("Invoice Builder"), encoder.Encode(user.Email!), key);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        QrCodeBase64 = Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(8));
    }

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }

        return sb.ToString().Trim().ToLowerInvariant();
    }
}
