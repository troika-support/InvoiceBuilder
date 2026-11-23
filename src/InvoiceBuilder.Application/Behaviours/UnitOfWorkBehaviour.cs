using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Behaviours;

/// <summary>
/// For <see cref="ITransactionalCommand"/> requests: opens a transaction, runs the handler, writes activity for
/// raised domain events, saves and commits. A failed Result rolls everything back.
/// </summary>
public sealed class UnitOfWorkBehaviour<TRequest, TResponse>(IUnitOfWork unitOfWork, IDomainEventDispatcher dispatcher)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not ITransactionalCommand)
        {
            return await next(cancellationToken);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var response = await next(cancellationToken);
        if (response.IsFailure)
        {
            return response;
        }

        await dispatcher.DispatchAsync(unitOfWork.TakeDomainEvents(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
