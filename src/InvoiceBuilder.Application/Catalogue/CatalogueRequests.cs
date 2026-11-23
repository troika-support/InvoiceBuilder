using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;

namespace InvoiceBuilder.Application.Catalogue;

public static class CatalogueErrors
{
    public static readonly Error NoBusiness = new("Catalogue.NoBusiness", "Select a business first.");

    public static readonly Error NotFound = new("Catalogue.NotFound", "That item no longer exists.");

    public static readonly Error InUse = new("Catalogue.InUse", "This item is used on documents. Archive it instead.");

    public static readonly Error DuplicateName = new("Catalogue.DuplicateName", "You already have an item with that name.");
}

public sealed record GetCatalogueQuery(string? Search, bool IncludeArchived = false) : IRequest<Result<IReadOnlyList<CatalogueItemRow>>>;

public sealed class GetCatalogueHandler(ICurrentBusiness currentBusiness, ICatalogueQueries queries)
    : IRequestHandler<GetCatalogueQuery, Result<IReadOnlyList<CatalogueItemRow>>>
{
    public async Task<Result<IReadOnlyList<CatalogueItemRow>>> Handle(GetCatalogueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return currentBusiness.BusinessId is null
            ? Result.Failure<IReadOnlyList<CatalogueItemRow>>(CatalogueErrors.NoBusiness)
            : Result.Success(await queries.ListAsync(request.Search, request.IncludeArchived, cancellationToken));
    }
}

/// <summary>"Add from catalogue…" in the document editor: top 10 active items whose name or description contains the term.</summary>
public sealed record SearchCatalogueQuery(string? Term) : IRequest<Result<IReadOnlyList<CatalogueSearchHit>>>
{
    public const int Take = 10;
}

public sealed class SearchCatalogueHandler(ICurrentBusiness currentBusiness, ICatalogueQueries queries)
    : IRequestHandler<SearchCatalogueQuery, Result<IReadOnlyList<CatalogueSearchHit>>>
{
    public async Task<Result<IReadOnlyList<CatalogueSearchHit>>> Handle(SearchCatalogueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return currentBusiness.BusinessId is null
            ? Result.Failure<IReadOnlyList<CatalogueSearchHit>>(CatalogueErrors.NoBusiness)
            : Result.Success(await queries.SearchAsync(request.Term?.Trim() ?? string.Empty, SearchCatalogueQuery.Take, cancellationToken));
    }
}

public sealed record GetTaxRateOptionsQuery : IRequest<Result<IReadOnlyList<TaxRateOption>>>;

public sealed class GetTaxRateOptionsHandler(ICurrentBusiness currentBusiness, ICatalogueQueries queries)
    : IRequestHandler<GetTaxRateOptionsQuery, Result<IReadOnlyList<TaxRateOption>>>
{
    public async Task<Result<IReadOnlyList<TaxRateOption>>> Handle(GetTaxRateOptionsQuery request, CancellationToken cancellationToken) =>
        currentBusiness.BusinessId is null
            ? Result.Failure<IReadOnlyList<TaxRateOption>>(CatalogueErrors.NoBusiness)
            : Result.Success(await queries.ListTaxRatesAsync(cancellationToken));
}

public sealed record CatalogueItemInput(
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string Currency,
    Guid? TaxRateId,
    string? HsCode,
    bool IsRecurring);

public sealed class CatalogueItemInputValidator : AbstractValidator<CatalogueItemInput>
{
    public CatalogueItemInputValidator()
    {
        RuleFor(i => i.Name).NotEmpty().WithMessage("Enter the item's name.").MaximumLength(200);
        RuleFor(i => i.Description).MaximumLength(1000);
        RuleFor(i => i.Unit).MaximumLength(40);
        RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0m).WithMessage("A unit price cannot be negative.");
        RuleFor(i => i.Currency).Must(c => CurrencyCode.Create(c).IsSuccess).WithMessage("Choose USD or ZiG.");
        RuleFor(i => i.HsCode).Must(h => HsCode.Create(h).IsSuccess).When(i => !string.IsNullOrWhiteSpace(i.HsCode))
            .WithMessage("An HS code is 1 to 8 digits.");
    }
}

internal static class CatalogueInputMapping
{
    public static Result<(CurrencyCode Currency, HsCode? HsCode)> Parse(CatalogueItemInput input)
    {
        var currency = CurrencyCode.Create(input.Currency);
        if (currency.IsFailure)
        {
            return Result.Failure<(CurrencyCode, HsCode?)>(currency.Error);
        }

        if (string.IsNullOrWhiteSpace(input.HsCode))
        {
            return Result.Success<(CurrencyCode, HsCode?)>((currency.Value, null));
        }

        var hs = HsCode.Create(input.HsCode);
        return hs.IsFailure
            ? Result.Failure<(CurrencyCode, HsCode?)>(hs.Error)
            : Result.Success<(CurrencyCode, HsCode?)>((currency.Value, hs.Value));
    }
}

public sealed record CreateCatalogueItemCommand(CatalogueItemInput Input) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateCatalogueItemValidator : AbstractValidator<CreateCatalogueItemCommand>
{
    public CreateCatalogueItemValidator() => RuleFor(c => c.Input).SetValidator(new CatalogueItemInputValidator());
}

public sealed class CreateCatalogueItemHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ICatalogueRepository items,
    ICatalogueQueries queries,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<CreateCatalogueItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(CatalogueErrors.NoBusiness);
        }

        var input = request.Input;
        if (await queries.NameExistsAsync(input.Name, null, cancellationToken))
        {
            return Result.Failure<Guid>(CatalogueErrors.DuplicateName);
        }
        var parsed = CatalogueInputMapping.Parse(input);
        if (parsed.IsFailure)
        {
            return Result.Failure<Guid>(parsed.Error);
        }

        var now = timeProvider.GetUtcNow();
        var created = CatalogueItem.Create(tenant.TenantId, businessId, input.Name, input.UnitPrice, parsed.Value.Currency, now, input.TaxRateId, parsed.Value.HsCode, input.IsRecurring);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var item = created.Value;
        item.Update(input.Name, input.Description, input.Unit, input.UnitPrice, parsed.Value.Currency, input.TaxRateId, parsed.Value.HsCode, input.IsRecurring, now);
        items.Add(item);
        await activity.AddAsync(nameof(CatalogueItem), item.Id, "CatalogueItemCreated", $"Catalogue item {item.Name} created.", cancellationToken);
        return Result.Success(item.Id);
    }
}

public sealed record UpdateCatalogueItemCommand(Guid ItemId, CatalogueItemInput Input) : ITransactionalCommand<Result>;

public sealed class UpdateCatalogueItemValidator : AbstractValidator<UpdateCatalogueItemCommand>
{
    public UpdateCatalogueItemValidator() => RuleFor(c => c.Input).SetValidator(new CatalogueItemInputValidator());
}

/// <summary>Editing an item never touches existing document lines: they hold their own snapshot (R13).</summary>
public sealed class UpdateCatalogueItemHandler(ICatalogueRepository items, ICatalogueQueries queries, IActivityLog activity, TimeProvider timeProvider)
    : IRequestHandler<UpdateCatalogueItemCommand, Result>
{
    public async Task<Result> Handle(UpdateCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await items.GetByIdAsync(request.ItemId, cancellationToken) is not { } item)
        {
            return Result.Failure(CatalogueErrors.NotFound);
        }

        var input = request.Input;
        if (await queries.NameExistsAsync(input.Name, item.Id, cancellationToken))
        {
            return Result.Failure(CatalogueErrors.DuplicateName);
        }
        var parsed = CatalogueInputMapping.Parse(input);
        if (parsed.IsFailure)
        {
            return Result.Failure(parsed.Error);
        }

        var updated = item.Update(input.Name, input.Description, input.Unit, input.UnitPrice, parsed.Value.Currency, input.TaxRateId, parsed.Value.HsCode, input.IsRecurring, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated;
        }

        await activity.AddAsync(nameof(CatalogueItem), item.Id, "CatalogueItemUpdated", $"Catalogue item {item.Name} updated.", cancellationToken);
        return Result.Success();
    }
}

public sealed record ArchiveCatalogueItemCommand(Guid ItemId, bool Archive = true) : ITransactionalCommand<Result>;

public sealed class ArchiveCatalogueItemHandler(ICatalogueRepository items, IActivityLog activity, TimeProvider timeProvider)
    : IRequestHandler<ArchiveCatalogueItemCommand, Result>
{
    public async Task<Result> Handle(ArchiveCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await items.GetByIdAsync(request.ItemId, cancellationToken) is not { } item)
        {
            return Result.Failure(CatalogueErrors.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        if (request.Archive)
        {
            item.Archive(now);
        }
        else
        {
            item.Restore(now);
        }

        var action = request.Archive ? "archived" : "restored";
        await activity.AddAsync(nameof(CatalogueItem), item.Id, request.Archive ? "CatalogueItemArchived" : "CatalogueItemRestored", $"Catalogue item {item.Name} {action}.", cancellationToken);
        return Result.Success();
    }
}

/// <summary>Only items never used on a document can be deleted; used ones can only be archived (R13).</summary>
public sealed record DeleteCatalogueItemCommand(Guid ItemId) : ITransactionalCommand<Result>;

public sealed class DeleteCatalogueItemHandler(ICatalogueRepository items, ICatalogueQueries queries, IActivityLog activity)
    : IRequestHandler<DeleteCatalogueItemCommand, Result>
{
    public async Task<Result> Handle(DeleteCatalogueItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await items.GetByIdAsync(request.ItemId, cancellationToken) is not { } item)
        {
            return Result.Failure(CatalogueErrors.NotFound);
        }

        if (await queries.IsUsedAsync(item.Id, cancellationToken))
        {
            return Result.Failure(CatalogueErrors.InUse);
        }

        items.Remove(item);
        await activity.AddAsync(nameof(CatalogueItem), item.Id, "CatalogueItemDeleted", $"Catalogue item {item.Name} deleted.", cancellationToken);
        return Result.Success();
    }
}
