using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;

namespace InvoiceBuilder.Application.Clients;

public static class ClientErrors
{
    public static readonly Error NoBusiness = new("Client.NoBusiness", "Select a business first.");

    public static readonly Error NotFound = new("Client.NotFound", "That client no longer exists.");

    public static readonly Error DuplicateName = new("Client.DuplicateName", "You already have a client with that name.");
}

public sealed record GetClientsQuery(string? Search, ClientFilter Filter, int Page = 1, int PageSize = 25) : IRequest<Result<ClientPage>>;

public sealed class GetClientsHandler(ICurrentBusiness currentBusiness, IClientQueries queries, TimeProvider timeProvider)
    : IRequestHandler<GetClientsQuery, Result<ClientPage>>
{
    public async Task<Result<ClientPage>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is null)
        {
            return Result.Failure<ClientPage>(ClientErrors.NoBusiness);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return Result.Success(await queries.ListAsync(request.Search, request.Filter, request.Page, request.PageSize, today, cancellationToken));
    }
}

public sealed record GetClientDetailQuery(Guid ClientId) : IRequest<Result<ClientDetail>>;

public sealed class GetClientDetailHandler(ICurrentBusiness currentBusiness, IClientQueries queries, TimeProvider timeProvider)
    : IRequestHandler<GetClientDetailQuery, Result<ClientDetail>>
{
    public async Task<Result<ClientDetail>> Handle(GetClientDetailQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is null)
        {
            return Result.Failure<ClientDetail>(ClientErrors.NoBusiness);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return await queries.GetDetailAsync(request.ClientId, today, cancellationToken) is { } detail
            ? Result.Success(detail)
            : Result.Failure<ClientDetail>(ClientErrors.NotFound);
    }
}

public sealed record ClientInput(
    string Name,
    string DefaultCurrency,
    string? Email,
    string? Phone,
    string? WhatsApp,
    string? Address,
    string? Notes);

public sealed class ClientInputValidator : AbstractValidator<ClientInput>
{
    public ClientInputValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Enter the client's name.").MaximumLength(200);
        RuleFor(c => c.DefaultCurrency).Must(c => CurrencyCode.Create(c).IsSuccess).WithMessage("Choose USD or ZiG.");
        RuleFor(c => c.Email).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email)).WithMessage("Enter a valid email address.").MaximumLength(254);
        RuleFor(c => c.Phone).MaximumLength(40);
        RuleFor(c => c.WhatsApp).MaximumLength(40);
        RuleFor(c => c.Address).MaximumLength(1000);
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

public sealed record CreateClientCommand(ClientInput Input) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientValidator() => RuleFor(c => c.Input).SetValidator(new ClientInputValidator());
}

public sealed class CreateClientHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    IClientRepository clients,
    IClientQueries queries,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<CreateClientCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(ClientErrors.NoBusiness);
        }

        var input = request.Input;
        if (await queries.NameExistsAsync(input.Name, null, cancellationToken))
        {
            return Result.Failure<Guid>(ClientErrors.DuplicateName);
        }

        var currency = CurrencyCode.Create(input.DefaultCurrency);
        if (currency.IsFailure)
        {
            return Result.Failure<Guid>(currency.Error);
        }

        var now = timeProvider.GetUtcNow();
        var created = Client.Create(tenant.TenantId, businessId, input.Name, currency.Value, now);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var client = created.Value;
        client.UpdateContact(input.Email, input.Phone, input.WhatsApp, input.Address, input.Notes, now);
        clients.Add(client);
        await activity.AddAsync(nameof(Client), client.Id, "ClientCreated", $"Client {client.Name} created.", cancellationToken);
        return Result.Success(client.Id);
    }
}

public sealed record UpdateClientCommand(Guid ClientId, ClientInput Input) : ITransactionalCommand<Result>;

public sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientValidator() => RuleFor(c => c.Input).SetValidator(new ClientInputValidator());
}

public sealed class UpdateClientHandler(
    IClientRepository clients,
    IClientQueries queries,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateClientCommand, Result>
{
    public async Task<Result> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await clients.GetByIdAsync(request.ClientId, cancellationToken) is not { } client)
        {
            return Result.Failure(ClientErrors.NotFound);
        }

        var input = request.Input;
        if (await queries.NameExistsAsync(input.Name, client.Id, cancellationToken))
        {
            return Result.Failure(ClientErrors.DuplicateName);
        }

        var currency = CurrencyCode.Create(input.DefaultCurrency);
        if (currency.IsFailure)
        {
            return Result.Failure(currency.Error);
        }

        var updated = client.Update(input.Name, currency.Value, input.Email, input.Phone, input.WhatsApp, input.Address, input.Notes, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated;
        }

        await activity.AddAsync(nameof(Client), client.Id, "ClientUpdated", $"Client {client.Name} updated.", cancellationToken);
        return Result.Success();
    }
}

public sealed record ArchiveClientCommand(Guid ClientId) : ITransactionalCommand<Result>;

public sealed class ArchiveClientHandler(IClientRepository clients, IActivityLog activity, TimeProvider timeProvider)
    : IRequestHandler<ArchiveClientCommand, Result>
{
    public async Task<Result> Handle(ArchiveClientCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await clients.GetByIdAsync(request.ClientId, cancellationToken) is not { } client)
        {
            return Result.Failure(ClientErrors.NotFound);
        }

        client.Archive(timeProvider.GetUtcNow());
        await activity.AddAsync(nameof(Client), client.Id, "ClientArchived", $"Client {client.Name} archived.", cancellationToken);
        return Result.Success();
    }
}
