using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvoiceBuilder.Web.Pages.Account;

public sealed class SignOutModel(SignInManager<AppUser> signIn) : PageModel
{
    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Account/SignIn");
    }
}
