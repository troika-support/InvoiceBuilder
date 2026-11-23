namespace InvoiceBuilder.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Collects and clears the domain events raised by tracked entities.</summary>
    IReadOnlyList<InvoiceBuilder.Domain.Common.IDomainEvent> TakeDomainEvents();
}

/// <summary>A database transaction. Disposing without committing rolls it back.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
