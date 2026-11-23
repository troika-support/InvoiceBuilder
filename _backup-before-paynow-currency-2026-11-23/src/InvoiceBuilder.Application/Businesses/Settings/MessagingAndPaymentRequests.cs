using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using MediatR;

namespace InvoiceBuilder.Application.Businesses.Settings;

/// <summary>Secrets never appear here: only whether one is saved.</summary>
public sealed record PaymentSettings(string PaynowIntegrationId, bool PaynowKeySaved, bool IsLive, string BankDetails);

public sealed record GetPaymentSettingsQuery : IRequest<Result<PaymentSettings>>;

public sealed class GetPaymentSettingsHandler(
    ICurrentBusiness currentBusiness,
    IPaymentGatewayConfigRepository gateways,
    IBusinessSettingsRepository settings) : IRequestHandler<GetPaymentSettingsQuery, Result<PaymentSettings>>
{
    public async Task<Result<PaymentSettings>> Handle(GetPaymentSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<PaymentSettings>(BusinessSettingsErrors.NoBusiness);
        }

        var gateway = await gateways.GetAsync(businessId, PaymentProvider.Paynow, cancellationToken);
        var other = await settings.GetAsync(businessId, cancellationToken);
        return Result.Success(new PaymentSettings(
            gateway?.IntegrationId ?? string.Empty,
            gateway is { IntegrationKeyEncrypted.Length: > 0 },
            gateway?.IsLive ?? false,
            other?.BankDetails ?? string.Empty));
    }
}

/// <summary>A blank <see cref="NewIntegrationKey"/> keeps the saved key.</summary>
public sealed record UpdatePaymentSettingsCommand(string PaynowIntegrationId, string? NewIntegrationKey, bool IsLive, string BankDetails)
    : ITransactionalCommand<Result>
{
    public override string ToString() => nameof(UpdatePaymentSettingsCommand);
}

public sealed class UpdatePaymentSettingsValidator : AbstractValidator<UpdatePaymentSettingsCommand>
{
    public UpdatePaymentSettingsValidator()
    {
        RuleFor(c => c.PaynowIntegrationId).MaximumLength(100);
        RuleFor(c => c.NewIntegrationKey).MaximumLength(200);
        RuleFor(c => c.BankDetails).MaximumLength(BusinessSettings.MaxBankDetailsLength).WithMessage("Use at most 1000 characters.");
    }
}

public sealed class UpdatePaymentSettingsHandler(
    ICurrentTenant currentTenant,
    ICurrentBusiness currentBusiness,
    IPaymentGatewayConfigRepository gateways,
    IBusinessSettingsRepository settings,
    IBusinessSecretStore secrets,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdatePaymentSettingsCommand, Result>
{
    public static readonly Error KeyRequired = new("Settings.PaynowKeyRequired", "Enter the Paynow integration key.");

    public static readonly Error IdRequired = new("Settings.PaynowIdRequired", "Enter the Paynow integration ID.");

    public async Task<Result> Handle(UpdatePaymentSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var changes = new List<string>();
        var hasId = !string.IsNullOrWhiteSpace(request.PaynowIntegrationId);
        var hasKey = !string.IsNullOrWhiteSpace(request.NewIntegrationKey);
        var gateway = await gateways.GetAsync(businessId, PaymentProvider.Paynow, cancellationToken);

        if (gateway is null && (hasId || hasKey))
        {
            if (!hasId)
            {
                return Result.Failure(IdRequired);
            }

            if (!hasKey)
            {
                return Result.Failure(KeyRequired);
            }

            var created = PaymentGatewayConfig.Create(
                currentTenant.TenantId, businessId, PaymentProvider.Paynow,
                request.PaynowIntegrationId, secrets.Protect(request.NewIntegrationKey!.Trim()), request.IsLive, now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            gateways.Add(created.Value);
            changes.Add($"Paynow connected ({(request.IsLive ? "live" : "test")})");
        }
        else if (gateway is not null)
        {
            if (!hasId)
            {
                return Result.Failure(IdRequired);
            }

            var modeChanged = gateway.IsLive != request.IsLive;
            var idChanged = gateway.IntegrationId != request.PaynowIntegrationId.Trim();
            gateway.Update(request.PaynowIntegrationId, hasKey ? secrets.Protect(request.NewIntegrationKey!.Trim()) : null, request.IsLive, now);
            if (hasKey)
            {
                changes.Add("Paynow key replaced");
            }

            if (idChanged)
            {
                changes.Add("Paynow integration ID changed");
            }

            if (modeChanged)
            {
                changes.Add($"Paynow switched to {(request.IsLive ? "live" : "test")}");
            }
        }

        var row = await settings.GetAsync(businessId, cancellationToken);
        if (row is null)
        {
            row = BusinessSettings.Create(currentTenant.TenantId, businessId, now);
            settings.Add(row);
        }

        if (row.BankDetails != request.BankDetails.Trim())
        {
            changes.Add("Bank details changed");
        }

        row.UpdateBankDetails(request.BankDetails, now);

        var summary = changes.Count == 0 ? "Payment settings saved" : $"Payment settings: {string.Join("; ", changes)}";
        await activity.AddAsync(nameof(Business), businessId, "PaymentSettingsUpdated", summary, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Checks the saved Paynow configuration can sign requests. Initiates nothing and calls no network.</summary>
public sealed record TestPaynowConnectionCommand : IRequest<Result>;

public sealed class TestPaynowConnectionHandler(
    ICurrentBusiness currentBusiness,
    IPaymentGatewayConfigRepository gateways,
    IBusinessSecretStore secrets) : IRequestHandler<TestPaynowConnectionCommand, Result>
{
    public static readonly Error NotConfigured = new("Settings.PaynowNotConfigured", "Save the integration ID and key first.");

    public static readonly Error KeyUnreadable = new("Settings.PaynowKeyUnreadable", "The saved key can't be read. Enter it again and save.");

    public async Task<Result> Handle(TestPaynowConnectionCommand request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var gateway = await gateways.GetAsync(businessId, PaymentProvider.Paynow, cancellationToken);
        if (gateway is null || string.IsNullOrWhiteSpace(gateway.IntegrationId))
        {
            return Result.Failure(NotConfigured);
        }

        try
        {
            return string.IsNullOrEmpty(secrets.Unprotect(gateway.IntegrationKeyEncrypted))
                ? Result.Failure(KeyUnreadable)
                : Result.Success();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Result.Failure(KeyUnreadable);
        }
    }
}

public sealed record MessagingSettings(string EmailFromName, string? EmailReplyTo, string? WhatsAppPhoneNumberId, bool WhatsAppTokenSaved);

public sealed record GetMessagingSettingsQuery : IRequest<Result<MessagingSettings>>;

public sealed class GetMessagingSettingsHandler(ICurrentBusiness currentBusiness, IBusinessSettingsRepository settings)
    : IRequestHandler<GetMessagingSettingsQuery, Result<MessagingSettings>>
{
    public async Task<Result<MessagingSettings>> Handle(GetMessagingSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<MessagingSettings>(BusinessSettingsErrors.NoBusiness);
        }

        var row = await settings.GetAsync(businessId, cancellationToken);
        return Result.Success(new MessagingSettings(
            row?.EmailFromName ?? string.Empty,
            row?.EmailReplyTo,
            row?.WhatsAppPhoneNumberId,
            row is { WhatsAppTokenEncrypted.Length: > 0 }));
    }
}

public sealed record UpdateMessagingSettingsCommand(
    string EmailFromName,
    string? EmailReplyTo,
    string? WhatsAppPhoneNumberId,
    string? NewWhatsAppToken,
    bool RemoveWhatsAppToken) : ITransactionalCommand<Result>
{
    public override string ToString() => nameof(UpdateMessagingSettingsCommand);
}

public sealed class UpdateMessagingSettingsValidator : AbstractValidator<UpdateMessagingSettingsCommand>
{
    public UpdateMessagingSettingsValidator()
    {
        RuleFor(c => c.EmailFromName).MaximumLength(200);
        RuleFor(c => c.EmailReplyTo).EmailAddress().WithMessage("Enter a valid email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.EmailReplyTo));
        RuleFor(c => c.WhatsAppPhoneNumberId).Matches("^[0-9]{5,30}$").WithMessage("Enter the numeric phone number ID from Meta.")
            .When(c => !string.IsNullOrWhiteSpace(c.WhatsAppPhoneNumberId));
        RuleFor(c => c.NewWhatsAppToken).MaximumLength(1000);
    }
}

public sealed class UpdateMessagingSettingsHandler(
    ICurrentTenant currentTenant,
    ICurrentBusiness currentBusiness,
    IBusinessSettingsRepository settings,
    IBusinessSecretStore secrets,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateMessagingSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateMessagingSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var row = await settings.GetAsync(businessId, cancellationToken);
        if (row is null)
        {
            row = BusinessSettings.Create(currentTenant.TenantId, businessId, now);
            settings.Add(row);
        }

        var hasToken = !string.IsNullOrWhiteSpace(request.NewWhatsAppToken);
        row.UpdateMessaging(
            request.EmailFromName,
            request.EmailReplyTo,
            request.WhatsAppPhoneNumberId,
            hasToken ? secrets.Protect(request.NewWhatsAppToken!.Trim()) : null,
            now);
        if (request.RemoveWhatsAppToken && !hasToken)
        {
            row.ClearWhatsAppToken(now);
        }

        var summary = "Messaging settings saved" + (hasToken ? "; WhatsApp token replaced" : request.RemoveWhatsAppToken ? "; WhatsApp token removed" : string.Empty);
        await activity.AddAsync(nameof(Business), businessId, "MessagingSettingsUpdated", summary, cancellationToken);
        return Result.Success();
    }
}

public sealed record ReminderSettings(bool Enabled, int DaysBeforeDue, bool OnDueDate, int RepeatEveryDays, int MaxAfterDue);

public sealed record GetReminderSettingsQuery : IRequest<Result<ReminderSettings>>;

public sealed class GetReminderSettingsHandler(ICurrentBusiness currentBusiness, IBusinessSettingsRepository settings)
    : IRequestHandler<GetReminderSettingsQuery, Result<ReminderSettings>>
{
    public async Task<Result<ReminderSettings>> Handle(GetReminderSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<ReminderSettings>(BusinessSettingsErrors.NoBusiness);
        }

        var row = await settings.GetAsync(businessId, cancellationToken) ?? BusinessSettings.Create(Guid.Empty, businessId, DateTimeOffset.UnixEpoch);
        return Result.Success(new ReminderSettings(
            row.RemindersEnabled, row.ReminderDaysBeforeDue, row.ReminderOnDueDate, row.ReminderRepeatEveryDays, row.ReminderMaxAfterDue));
    }
}

/// <summary>R30: before due, on the due date, then every N days up to a maximum.</summary>
public sealed record UpdateReminderSettingsCommand(bool Enabled, int DaysBeforeDue, bool OnDueDate, int RepeatEveryDays, int MaxAfterDue)
    : ITransactionalCommand<Result>;

public sealed class UpdateReminderSettingsValidator : AbstractValidator<UpdateReminderSettingsCommand>
{
    public UpdateReminderSettingsValidator()
    {
        RuleFor(c => c.DaysBeforeDue).InclusiveBetween(0, 60).WithMessage("Enter 0 to 60 days.");
        RuleFor(c => c.RepeatEveryDays).InclusiveBetween(1, 60).WithMessage("Enter 1 to 60 days.");
        RuleFor(c => c.MaxAfterDue).InclusiveBetween(0, 10).WithMessage("Enter 0 to 10 reminders.");
    }
}

public sealed class UpdateReminderSettingsHandler(
    ICurrentTenant currentTenant,
    ICurrentBusiness currentBusiness,
    IBusinessSettingsRepository settings,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateReminderSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var row = await settings.GetAsync(businessId, cancellationToken);
        if (row is null)
        {
            row = BusinessSettings.Create(currentTenant.TenantId, businessId, now);
            settings.Add(row);
        }

        var result = row.UpdateReminders(request.Enabled, request.DaysBeforeDue, request.OnDueDate, request.RepeatEveryDays, request.MaxAfterDue, now);
        if (result.IsFailure)
        {
            return result;
        }

        var summary = request.Enabled
            ? $"Reminders on: {request.DaysBeforeDue} days before, {(request.OnDueDate ? "on the due date, " : string.Empty)}then every {request.RepeatEveryDays} days up to {request.MaxAfterDue}"
            : "Reminders turned off";
        await activity.AddAsync(nameof(Business), businessId, "ReminderSettingsUpdated", summary, cancellationToken);
        return Result.Success();
    }
}
