using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

public static class DraftDocumentErrors
{
    public static readonly Error NoBusiness = new("Document.NoBusiness", "Select a business first.");

    public static readonly Error NotFound = new("Document.NotFound", "That document no longer exists.");

    public static readonly Error ClientNotFound = new("Document.ClientNotFound", "Choose a client.");

    public static readonly Error WrongType = new("Document.TypeNotEditable", "Only quotes and invoices are edited here.");

    public static readonly Error TaxRateNotFound = new("Document.TaxRateNotFound", "That tax rate is no longer available.");
}

/// <summary>Loads a draft the current business owns; anything else reads as not found (R2).</summary>
internal static class DraftLoader
{
    public static async Task<Result<Document>> LoadAsync(ICurrentBusiness business, IDocumentRepository documents, Guid id, CancellationToken cancellationToken)
    {
        if (business.BusinessId is not { } businessId)
        {
            return Result.Failure<Document>(DraftDocumentErrors.NoBusiness);
        }

        var document = await documents.GetByIdAsync(id, cancellationToken);
        return document is null || document.BusinessId != businessId
            ? Result.Failure<Document>(DraftDocumentErrors.NotFound)
            : Result.Success(document);
    }
}

public sealed record DraftTotals(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total)
{
    public static DraftTotals From(Document d) => new(d.Subtotal, d.DiscountTotal, d.TaxTotal, d.Total);
}

// ---------- Create ----------

public sealed record CreateDraftDocumentCommand(DocumentType Type, Guid ClientId) : ITransactionalCommand<Result<Guid>>;

public sealed class CreateDraftDocumentValidator : AbstractValidator<CreateDraftDocumentCommand>
{
    public CreateDraftDocumentValidator()
    {
        RuleFor(c => c.Type).Must(t => t is DocumentType.Invoice or DocumentType.Quote).WithMessage("Only quotes and invoices are created here.");
        RuleFor(c => c.ClientId).NotEmpty().WithMessage("Choose a client.");
    }
}

public sealed class CreateDraftDocumentHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    IClientRepository clients,
    IDocumentRepository documents,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<CreateDraftDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDraftDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(DraftDocumentErrors.NoBusiness);
        }

        var client = await clients.GetByIdAsync(request.ClientId, cancellationToken);
        if (client is null || client.IsArchived)
        {
            return Result.Failure<Guid>(DraftDocumentErrors.ClientNotFound);
        }

        var document = Document.CreateDraft(tenant.TenantId, businessId, request.Type, client.Id, client.DefaultCurrency, timeProvider.GetUtcNow());
        documents.Add(document);
        await activity.AddAsync(nameof(Document), document.Id, "DraftCreated", $"Draft {request.Type} created for {client.Name}.", cancellationToken);
        return Result.Success(document.Id);
    }
}

// ---------- Header ----------

public sealed record UpdateDraftHeaderCommand(
    Guid DocumentId,
    Guid ClientId,
    string Currency,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateOnly? ValidUntil,
    string? Notes,
    string? Terms,
    AllowedPaymentMethods PaymentMethods) : ITransactionalCommand<Result>;

public sealed class UpdateDraftHeaderValidator : AbstractValidator<UpdateDraftHeaderCommand>
{
    public UpdateDraftHeaderValidator()
    {
        RuleFor(c => c.ClientId).NotEmpty().WithMessage("Choose a client.");
        RuleFor(c => c.Currency).Must(c => CurrencyCode.Create(c).IsSuccess).WithMessage("Choose USD or ZiG.");
        RuleFor(c => c.Notes).MaximumLength(2000);
        RuleFor(c => c.Terms).MaximumLength(2000);
    }
}

public sealed class UpdateDraftHeaderHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IClientRepository clients,
    TimeProvider timeProvider) : IRequestHandler<UpdateDraftHeaderCommand, Result>
{
    public async Task<Result> Handle(UpdateDraftHeaderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        var document = loaded.Value;
        if (request.ClientId != document.ClientId)
        {
            var client = await clients.GetByIdAsync(request.ClientId, cancellationToken);
            if (client is null || client.IsArchived)
            {
                return Result.Failure(DraftDocumentErrors.ClientNotFound);
            }
        }

        var currency = CurrencyCode.Create(request.Currency);
        if (currency.IsFailure)
        {
            return currency;
        }

        var now = timeProvider.GetUtcNow();
        var header = document.UpdateDraftHeader(request.ClientId, currency.Value, request.IssueDate, request.DueDate, request.ValidUntil, now);
        if (header.IsFailure)
        {
            return header;
        }

        var notes = document.SetNotes(request.Notes, request.Terms, now);
        return notes.IsFailure ? notes : document.SetAllowedPaymentMethods(request.PaymentMethods, now);
    }
}

// ---------- Lines ----------

/// <summary>One editor row. Tax is chosen by rate ID and snapshotted on save (R13); the HS code is copied as typed (R13a).</summary>
public sealed record DraftLineInput(
    string Description,
    string? Detail,
    decimal Quantity,
    decimal UnitPrice,
    decimal? LineDiscount,
    Guid? TaxRateId,
    string? HsCode,
    Guid? CatalogueItemId = null);

public sealed class DraftLineInputValidator : AbstractValidator<DraftLineInput>
{
    public DraftLineInputValidator()
    {
        RuleFor(l => l.Description).NotEmpty().WithMessage("Enter a description.").MaximumLength(500);
        RuleFor(l => l.Detail).MaximumLength(1000);
        RuleFor(l => l.Quantity).GreaterThan(0m).WithMessage("Quantity must be more than 0.");
        RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m).WithMessage("A unit price cannot be negative.");
        RuleFor(l => l.LineDiscount).GreaterThanOrEqualTo(0m).When(l => l.LineDiscount is not null).WithMessage("A discount cannot be negative.");
        RuleFor(l => l.HsCode).Must(h => HsCode.Create(h).IsSuccess).When(l => !string.IsNullOrWhiteSpace(l.HsCode))
            .WithMessage("An HS code is 1 to 8 digits.");
    }
}

/// <summary>Replaces the full line list in the given order (autosave sends the whole table).</summary>
public sealed record UpsertDraftLinesCommand(Guid DocumentId, IReadOnlyList<DraftLineInput> Lines) : ITransactionalCommand<Result<DraftTotals>>
{
    public const int MaxLines = 200;
}

public sealed class UpsertDraftLinesValidator : AbstractValidator<UpsertDraftLinesCommand>
{
    public UpsertDraftLinesValidator()
    {
        RuleFor(c => c.Lines).NotNull().Must(l => l.Count <= UpsertDraftLinesCommand.MaxLines)
            .WithMessage($"A document can have at most {UpsertDraftLinesCommand.MaxLines} lines.");
        RuleForEach(c => c.Lines).SetValidator(new DraftLineInputValidator());
    }
}

public sealed class UpsertDraftLinesHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IBusinessRepository businesses,
    TimeProvider timeProvider) : IRequestHandler<UpsertDraftLinesCommand, Result<DraftTotals>>
{
    public async Task<Result<DraftTotals>> Handle(UpsertDraftLinesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DraftTotals>(loaded.Error);
        }

        var document = loaded.Value;
        if (!document.IsEditable)
        {
            return Result.Failure<DraftTotals>(DocumentErrors.NotEditable);
        }

        Dictionary<Guid, TaxRate> rates = request.Lines.Any(l => l.TaxRateId is not null)
            ? (await businesses.ListTaxRatesAsync(document.BusinessId, cancellationToken)).ToDictionary(r => r.Id)
            : [];

        var inputs = new List<LineInput>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            TaxRate? rate = null;
            if (line.TaxRateId is { } rateId && !rates.TryGetValue(rateId, out rate))
            {
                return Result.Failure<DraftTotals>(DraftDocumentErrors.TaxRateNotFound);
            }

            HsCode? hs = null;
            if (!string.IsNullOrWhiteSpace(line.HsCode))
            {
                var parsed = HsCode.Create(line.HsCode);
                if (parsed.IsFailure)
                {
                    return Result.Failure<DraftTotals>(parsed.Error);
                }

                hs = parsed.Value;
            }

            inputs.Add(new LineInput(line.Description.Trim(), line.Detail, line.Quantity, line.UnitPrice, line.LineDiscount ?? 0m, rate, hs, line.CatalogueItemId));
        }

        var replaced = document.ReplaceLines(inputs, timeProvider.GetUtcNow());
        return replaced.IsFailure ? Result.Failure<DraftTotals>(replaced.Error) : Result.Success(DraftTotals.From(document));
    }
}

// ---------- Discount ----------

public sealed record SetDocumentDiscountCommand(Guid DocumentId, DiscountKind Kind, decimal Value) : ITransactionalCommand<Result<DraftTotals>>;

public sealed class SetDocumentDiscountHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    TimeProvider timeProvider) : IRequestHandler<SetDocumentDiscountCommand, Result<DraftTotals>>
{
    public async Task<Result<DraftTotals>> Handle(SetDocumentDiscountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<DraftTotals>(loaded.Error);
        }

        var discount = request.Kind switch
        {
            DiscountKind.Percent => Discount.OfPercent(request.Value),
            DiscountKind.Fixed => Discount.OfFixed(request.Value),
            _ => Result.Success(Discount.None),
        };
        if (discount.IsFailure)
        {
            return Result.Failure<DraftTotals>(discount.Error);
        }

        var set = loaded.Value.SetDiscount(discount.Value, timeProvider.GetUtcNow());
        return set.IsFailure ? Result.Failure<DraftTotals>(set.Error) : Result.Success(DraftTotals.From(loaded.Value));
    }
}

// ---------- Instalment plan (R34, R35) ----------

public sealed record SetInstalmentPlanCommand(Guid DocumentId, IReadOnlyList<InstalmentDraft> Instalments, InstalmentFrequency? Frequency)
    : ITransactionalCommand<Result>;

public sealed class SetInstalmentPlanValidator : AbstractValidator<SetInstalmentPlanCommand>
{
    public SetInstalmentPlanValidator()
    {
        RuleFor(c => c.Instalments).NotNull()
            .Must(i => i.Count is >= InstalmentPlanner.MinCount and <= InstalmentPlanner.MaxCount)
            .WithMessage($"A plan has {InstalmentPlanner.MinCount} to {InstalmentPlanner.MaxCount} instalments.");
    }
}

public sealed class SetInstalmentPlanHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    TimeProvider timeProvider) : IRequestHandler<SetInstalmentPlanCommand, Result>
{
    public async Task<Result> Handle(SetInstalmentPlanCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        return loaded.IsFailure ? loaded : loaded.Value.SetPlan(request.Instalments, request.Frequency, timeProvider.GetUtcNow());
    }
}

public sealed record ClearInstalmentPlanCommand(Guid DocumentId) : ITransactionalCommand<Result>;

public sealed class ClearInstalmentPlanHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    TimeProvider timeProvider) : IRequestHandler<ClearInstalmentPlanCommand, Result>
{
    public async Task<Result> Handle(ClearInstalmentPlanCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        return loaded.IsFailure ? loaded : loaded.Value.ClearPlan(timeProvider.GetUtcNow());
    }
}

// ---------- Duplicate ----------

public sealed record DuplicateDocumentCommand(Guid DocumentId) : ITransactionalCommand<Result<Guid>>;

public sealed class DuplicateDocumentHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<DuplicateDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(DuplicateDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<Guid>(loaded.Error);
        }

        var source = loaded.Value;
        if (source.Type is not (DocumentType.Invoice or DocumentType.Quote))
        {
            return Result.Failure<Guid>(DraftDocumentErrors.WrongType);
        }

        var copy = source.Duplicate(timeProvider.GetUtcNow());
        documents.Add(copy);
        var label = source.Number?.Value ?? "a draft";
        await activity.AddAsync(nameof(Document), copy.Id, "Duplicated", $"Draft {copy.Type} duplicated from {label}.", cancellationToken);
        return Result.Success(copy.Id);
    }
}
