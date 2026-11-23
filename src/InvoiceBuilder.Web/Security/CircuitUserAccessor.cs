using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace InvoiceBuilder.Web.Security;

/// <summary>
/// Holds the signed-in user for a Blazor circuit. Inside a circuit HttpContext is not reliable,
/// so WebCurrentScope reads this first and falls back to HttpContext for HTTP requests.
/// </summary>
public sealed class CircuitUserAccessor
{
    public ClaimsPrincipal? User { get; set; }
}

public sealed class CircuitUserHandler(AuthenticationStateProvider authentication, CircuitUserAccessor accessor) : CircuitHandler, IDisposable
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        authentication.AuthenticationStateChanged += OnChanged;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            accessor.User = (await authentication.GetAuthenticationStateAsync()).User;
            await next(context);
        };

    public void Dispose() => authentication.AuthenticationStateChanged -= OnChanged;

    private void OnChanged(Task<AuthenticationState> task) =>
        _ = UpdateAsync(task);

    private async Task UpdateAsync(Task<AuthenticationState> task) =>
        accessor.User = (await task).User;
}
