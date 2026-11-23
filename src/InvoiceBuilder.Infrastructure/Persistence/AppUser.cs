using Microsoft.AspNetCore.Identity;

namespace InvoiceBuilder.Infrastructure.Persistence;

public sealed class AppUser : IdentityUser
{
    public Guid TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The business chosen in the sidebar switcher, stored per user (R2).</summary>
    public Guid? SelectedBusinessId { get; set; }

    /// <summary>Dark mode choice; null follows the operating system (prompt 09).</summary>
    public bool? PrefersDarkMode { get; set; }
}
