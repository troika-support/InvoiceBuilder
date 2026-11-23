using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Abstractions;

/// <summary>Marks a command whose handler runs in one transaction that is saved and committed on success.</summary>
public interface ITransactionalCommand;

public interface ITransactionalCommand<TResponse> : IRequest<TResponse>, ITransactionalCommand
    where TResponse : Result;
