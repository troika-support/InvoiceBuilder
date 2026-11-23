using InvoiceBuilder.Application.Shell;
using InvoiceBuilder.Web.Security;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace InvoiceBuilder.Web.Services;

/// <summary>Thin UI service for the app frame: business list, selection and dark mode (prompt 09).</summary>
public sealed class ShellState(ISender sender, AuthenticationStateProvider authentication)
{
    public ShellInfo? Info { get; private set; }

    public ShellBusiness? Current => Info?.Businesses.FirstOrDefault(b => b.Id == Info.SelectedBusinessId);

    private Task? _loading;

    /// <summary>Concurrent callers share one load; the scoped DbContext allows one operation at a time.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        _loading ??= LoadCoreAsync(cancellationToken);

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            var result = await sender.Send(new GetShellQuery(await IsOwnerAsync()), cancellationToken);
            Info = result.IsSuccess ? result.Value : null;
        }
        finally
        {
            _loading = null;
        }
    }

    public async Task<bool> SelectBusinessAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new SelectBusinessCommand(businessId, await IsOwnerAsync()), cancellationToken);
        return result.IsSuccess;
    }

    public async Task SetDarkModeAsync(bool? dark, CancellationToken cancellationToken = default)
    {
        await sender.Send(new SetDarkModeCommand(dark), cancellationToken);
        if (Info is not null)
        {
            Info = Info with { DarkMode = dark };
        }
    }

    private async Task<bool> IsOwnerAsync() =>
        (await authentication.GetAuthenticationStateAsync()).User.IsInRole(Roles.Owner);
}

/// <summary>Screen-reader announcements (aria-live polite) with an optional snackbar toast (DS §6 Toast).</summary>
public sealed class LiveAnnouncer(ISnackbar snackbar)
{
    public event Action<string>? Announced;

    public void Announce(string message) => Announced?.Invoke(message);

    public void Toast(string message, Severity severity = Severity.Normal)
    {
        snackbar.Add(message, severity);
        Announce(message);
    }
}
