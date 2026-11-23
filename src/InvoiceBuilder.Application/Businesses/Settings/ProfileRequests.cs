using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Templates;
using MediatR;

namespace InvoiceBuilder.Application.Businesses.Settings;

public static class BusinessSettingsErrors
{
    public static readonly Error NoBusiness = new("Settings.NoBusiness", "Choose a business first.");

    public static readonly Error NotSignedIn = new("Settings.NotSignedIn", "Sign in again to continue.");
}

public sealed record NumberingRow(DocumentType DocumentType, string Prefix, long NextValue, int Padding);

public sealed record BusinessProfile(
    Guid BusinessId,
    string TradingName,
    string RegisteredName,
    string Email,
    string? WhatsAppNumber,
    string Address,
    byte[]? Logo,
    string? LogoContentType,
    IReadOnlyList<NumberingRow> Numbering);

/// <summary>Profile &amp; numbering section of S10 for the current business (DS §8.10).</summary>
public sealed record GetBusinessProfileQuery : IRequest<Result<BusinessProfile>>;

public sealed class GetBusinessProfileHandler(
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    INumberSequenceRepository sequences) : IRequestHandler<GetBusinessProfileQuery, Result<BusinessProfile>>
{
    public async Task<Result<BusinessProfile>> Handle(GetBusinessProfileQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId
            || await businesses.GetByIdAsync(businessId, cancellationToken) is not { } business)
        {
            return Result.Failure<BusinessProfile>(BusinessSettingsErrors.NoBusiness);
        }

        var numbering = (await sequences.ListAsync(businessId, cancellationToken))
            .OrderBy(s => s.DocumentType)
            .Select(s => new NumberingRow(s.DocumentType, s.Prefix, s.NextValue, s.Padding))
            .ToList();

        return Result.Success(new BusinessProfile(
            business.Id,
            business.TradingName,
            business.RegisteredName,
            business.Email,
            business.WhatsAppNumber,
            business.Address,
            business.Logo,
            business.LogoContentType,
            numbering));
    }
}

/// <summary>New logo bytes; null keeps the current logo. <see cref="Remove"/> clears it.</summary>
public sealed record LogoChange(byte[]? Content, string? ContentType, bool Remove);

public sealed record UpdateBusinessProfileCommand(
    string TradingName,
    string RegisteredName,
    string Email,
    string? WhatsAppNumber,
    string Address,
    LogoChange? Logo,
    IReadOnlyDictionary<DocumentType, string> Prefixes) : ITransactionalCommand<Result>;

public sealed class UpdateBusinessProfileValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileValidator()
    {
        RuleFor(c => c.TradingName).NotEmpty().WithMessage("Enter a trading name.").MaximumLength(200);
        RuleFor(c => c.RegisteredName).MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().WithMessage("Enter an email address.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(c => c.WhatsAppNumber).Matches(@"^\+?[0-9 ]{7,20}$").When(c => !string.IsNullOrWhiteSpace(c.WhatsAppNumber))
            .WithMessage("Enter the number with country code, for example +263 77 123 4567.");
        RuleFor(c => c.Address).MaximumLength(1000);
        RuleForEach(c => c.Prefixes.Values)
            .NotEmpty().WithMessage("Enter a prefix.")
            .MaximumLength(NumberSequence.MaxPrefixLength)
            .Must(p => !p.Any(char.IsWhiteSpace)).WithMessage("Prefixes can't contain spaces.")
            .OverridePropertyName("Prefixes");
    }
}

public sealed class UpdateBusinessProfileHandler(
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    INumberSequenceRepository sequences,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateBusinessProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateBusinessProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();

        if (currentBusiness.BusinessId is not { } businessId
            || await businesses.GetByIdAsync(businessId, cancellationToken) is not { } business)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var details = business.UpdateDetails(
            request.TradingName,
            request.RegisteredName.Trim(),
            request.Email.Trim(),
            string.IsNullOrWhiteSpace(request.WhatsAppNumber) ? null : request.WhatsAppNumber.Trim(),
            request.Address.Trim(),
            business.LogoAssetId,
            now);
        if (details.IsFailure)
        {
            return details;
        }

        if (request.Logo is { Remove: true })
        {
            business.RemoveLogo(now);
        }
        else if (request.Logo is { Content: { } content, ContentType: { } contentType })
        {
            var logo = business.SetLogo(content, contentType, now);
            if (logo.IsFailure)
            {
                return logo;
            }
        }

        var changedPrefixes = new List<string>();
        foreach (var sequence in await sequences.ListAsync(businessId, cancellationToken))
        {
            if (request.Prefixes.TryGetValue(sequence.DocumentType, out var prefix) && prefix != sequence.Prefix)
            {
                var old = sequence.Prefix;
                var changed = sequence.ChangePrefix(prefix, now);
                if (changed.IsFailure)
                {
                    return changed;
                }

                changedPrefixes.Add($"{sequence.DocumentType} {old} → {prefix}");
            }
        }

        var summary = changedPrefixes.Count == 0
            ? "Profile updated"
            : $"Profile updated; prefixes changed: {string.Join(", ", changedPrefixes)}";
        await activity.AddAsync(nameof(Business), business.Id, "ProfileUpdated", summary, cancellationToken);
        return Result.Success();
    }
}

/// <summary>"+ Add a business": default sequences (R6) and the default theme, then selected for the user.</summary>
public sealed record AddBusinessCommand(string TradingName, string RegisteredName, string Email, string Address)
    : ITransactionalCommand<Result<Guid>>;

public sealed class AddBusinessValidator : AbstractValidator<AddBusinessCommand>
{
    public AddBusinessValidator()
    {
        RuleFor(c => c.TradingName).NotEmpty().WithMessage("Enter a trading name.").MaximumLength(200);
        RuleFor(c => c.RegisteredName).MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().WithMessage("Enter an email address.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(c => c.Address).MaximumLength(1000);
    }
}

public sealed class AddBusinessHandler(
    ICurrentTenant tenant,
    ICurrentUser user,
    IBusinessRepository businesses,
    INumberSequenceRepository sequences,
    ITemplateRepository templates,
    IUserPreferenceStore preferences,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<AddBusinessCommand, Result<Guid>>
{
    public static readonly Error DuplicateName = new("Business.DuplicateName", "You already have a business with this trading name.");

    public async Task<Result<Guid>> Handle(AddBusinessCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (user.UserId is not { } userId)
        {
            return Result.Failure<Guid>(BusinessSettingsErrors.NotSignedIn);
        }

        var name = request.TradingName.Trim();
        if ((await businesses.ListAsync(cancellationToken)).Any(b => string.Equals(b.TradingName, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<Guid>(DuplicateName);
        }

        var now = timeProvider.GetUtcNow();
        var business = Business.Create(tenant.TenantId, name, request.RegisteredName.Trim(), request.Email.Trim(), request.Address.Trim(), now);
        businesses.Add(business);

        foreach (var type in new[] { DocumentType.Quote, DocumentType.Invoice, DocumentType.Receipt, DocumentType.CreditNote })
        {
            sequences.Add(NumberSequence.CreateDefault(tenant.TenantId, business.Id, type, now));
        }

        var theme = Theme.Create(tenant.TenantId, business.Id, "Default", "{}", now);
        if (theme.IsFailure)
        {
            return Result.Failure<Guid>(theme.Error);
        }

        templates.AddTheme(theme.Value);
        var defaults = business.UpdateDefaults(
            business.DefaultCurrency,
            business.DefaultTermsDays,
            business.QuoteValidityDays,
            business.AllowPartPayments,
            business.MinPartPaymentPercent,
            theme.Value.Id,
            now);
        if (defaults.IsFailure)
        {
            return Result.Failure<Guid>(defaults.Error);
        }

        await activity.AddAsync(nameof(Business), business.Id, "BusinessAdded", $"Business {name} added", cancellationToken);
        await preferences.SetSelectedBusinessAsync(userId, business.Id, cancellationToken);
        return Result.Success(business.Id);
    }
}
