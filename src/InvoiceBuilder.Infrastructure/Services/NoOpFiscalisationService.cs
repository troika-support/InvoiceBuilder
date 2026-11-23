using InvoiceBuilder.Application.Abstractions;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>Phase 1 (R32): nothing is fiscalised. Prompt 29 replaces this; the interface stays.</summary>
public sealed class NoOpFiscalisationService : IFiscalisationService
{
    public Task QueueAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
