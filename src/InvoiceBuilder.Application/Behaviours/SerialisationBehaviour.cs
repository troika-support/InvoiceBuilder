using MediatR;

namespace InvoiceBuilder.Application.Behaviours;

/// <summary>One gate per DI scope (a Blazor circuit); the scoped DbContext allows one operation at a time.</summary>
public sealed class RequestGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public void Dispose() => Semaphore.Dispose();
}

/// <summary>Runs requests in the same scope one at a time so concurrent components never share a DbContext operation.</summary>
public sealed class SerialisationBehaviour<TRequest, TResponse>(RequestGate gate)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Nested requests sent from within a handler already run inside the gate; re-entering would deadlock.
        if (SerialisationGateState.Held.Value)
        {
            return await next(cancellationToken);
        }

        await gate.Semaphore.WaitAsync(cancellationToken);
        SerialisationGateState.Held.Value = true;
        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            SerialisationGateState.Held.Value = false;
            gate.Semaphore.Release();
        }
    }
}

/// <summary>Non-generic holder so the flag is shared across all closed generic behaviours.</summary>
internal static class SerialisationGateState
{
    internal static readonly AsyncLocal<bool> Held = new();
}
