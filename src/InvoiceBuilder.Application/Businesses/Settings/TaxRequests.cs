using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;

namespace InvoiceBuilder.Application.Businesses.Settings;

public sealed record TaxRateRow(Guid Id, string Name, decimal Percent, DateOnly EffectiveFrom, string? FiscalTaxId);

public sealed record TaxAndCurrency(
    string DefaultCurrency,
    int DefaultTermsDays,
    int QuoteValidityDays,
    bool AllowPartPayments,
    decimal MinPartPaymentPercent,
    IReadOnlyList<TaxRateRow> TaxRates);

/// <summary>Tax &amp; currency section of S10 (R7, R9, R13, R20).</summary>
public sealed record GetTaxAndCurrencyQuery : IRequest<Result<TaxAndCurrency>>;

public sealed class GetTaxAndCurrencyHandler(ICurrentBusiness currentBusiness, IBusinessRepository businesses)
    : IRequestHandler<GetTaxAndCurrencyQuery, Result<TaxAndCurrency>>
{
    public async Task<Result<TaxAndCurrency>> Handle(GetTaxAndCurrencyQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId
            || await businesses.GetByIdAsync(businessId, cancellationToken) is not { } business)
        {
            return Result.Failure<TaxAndCurrency>(BusinessSettingsErrors.NoBusiness);
        }

        var rates = (await businesses.ListTaxRatesAsync(businessId, cancellationToken))
            .Where(r => !r.IsArchived)
            .Select(r => new TaxRateRow(r.Id, r.Name, r.Percent.Value, r.EffectiveFrom, r.FiscalTaxId))
            .ToList();

        return Result.Success(new TaxAndCurrency(
            business.DefaultCurrency.Code,
            business.DefaultTermsDays,
            business.QuoteValidityDays,
            business.AllowPartPayments,
            business.MinPartPaymentPercent,
            rates));
    }
}

public sealed record NewTaxRate(string Name, decimal Percent, DateOnly EffectiveFrom, string? FiscalTaxId);

public sealed record UpdateTaxAndCurrencyCommand(
    string DefaultCurrency,
    int DefaultTermsDays,
    int QuoteValidityDays,
    bool AllowPartPayments,
    decimal MinPartPaymentPercent,
    IReadOnlyList<NewTaxRate> AddRates,
    IReadOnlyList<Guid> ArchiveRateIds) : ITransactionalCommand<Result>;

public sealed class UpdateTaxAndCurrencyValidator : AbstractValidator<UpdateTaxAndCurrencyCommand>
{
    public UpdateTaxAndCurrencyValidator()
    {
        RuleFor(c => c.DefaultCurrency).Must(c => CurrencyCode.Create(c).IsSuccess).WithMessage("Choose USD or ZiG.");
        RuleFor(c => c.DefaultTermsDays).InclusiveBetween(0, 365).WithMessage("Enter 0 to 365 days.");
        RuleFor(c => c.QuoteValidityDays).InclusiveBetween(1, 365).WithMessage("Enter 1 to 365 days.");
        RuleFor(c => c.MinPartPaymentPercent).InclusiveBetween(0m, 100m).WithMessage("Enter a percentage from 0 to 100.");
        RuleForEach(c => c.AddRates).ChildRules(r =>
        {
            r.RuleFor(x => x.Name).NotEmpty().WithMessage("Enter a name for the tax rate.").MaximumLength(100);
            r.RuleFor(x => x.Percent).InclusiveBetween(0m, 100m).WithMessage("Enter a percentage from 0 to 100.");
            r.RuleFor(x => x.FiscalTaxId).MaximumLength(50);
        });
    }
}

public sealed class UpdateTaxAndCurrencyHandler(
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateTaxAndCurrencyCommand, Result>
{
    public async Task<Result> Handle(UpdateTaxAndCurrencyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();

        if (currentBusiness.BusinessId is not { } businessId
            || await businesses.GetByIdAsync(businessId, cancellationToken) is not { } business)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var currency = CurrencyCode.Create(request.DefaultCurrency);
        if (currency.IsFailure)
        {
            return Result.Failure(currency.Error);
        }

        var defaults = business.UpdateDefaults(
            currency.Value,
            request.DefaultTermsDays,
            request.QuoteValidityDays,
            request.AllowPartPayments,
            request.MinPartPaymentPercent,
            business.DefaultThemeId,
            now);
        if (defaults.IsFailure)
        {
            return defaults;
        }

        var existing = await businesses.ListTaxRatesAsync(businessId, cancellationToken);
        foreach (var rate in existing.Where(r => !r.IsArchived && request.ArchiveRateIds.Contains(r.Id)))
        {
            rate.Archive(now);
        }

        foreach (var add in request.AddRates)
        {
            var percent = TaxPercent.Create(add.Percent);
            if (percent.IsFailure)
            {
                return Result.Failure(percent.Error);
            }

            businesses.AddTaxRate(TaxRate.Create(business.TenantId, businessId, add.Name, percent.Value, add.EffectiveFrom, add.FiscalTaxId, now));
        }

        var summary = $"Tax & currency updated: {currency.Value.Code}, terms {request.DefaultTermsDays} days, quotes valid {request.QuoteValidityDays} days, "
            + (request.AllowPartPayments ? $"part payments from {request.MinPartPaymentPercent}%" : "no part payments")
            + (request.AddRates.Count > 0 ? $"; added {string.Join(", ", request.AddRates.Select(r => $"{r.Name} {r.Percent}%"))}" : string.Empty)
            + (request.ArchiveRateIds.Count > 0 ? $"; archived {request.ArchiveRateIds.Count} rate(s)" : string.Empty);
        await activity.AddAsync(nameof(Business), business.Id, "TaxSettingsUpdated", summary, cancellationToken);
        return Result.Success();
    }
}
