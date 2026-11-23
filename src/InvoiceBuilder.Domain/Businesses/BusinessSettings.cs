using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Businesses;

/// <summary>
/// Per-business settings that are not part of the profile: bank details, messaging and reminders (R30).
/// The WhatsApp token is stored encrypted and never exposed.
/// </summary>
public sealed class BusinessSettings : BusinessEntity
{
    public const int MaxBankDetailsLength = 1000;

    private BusinessSettings()
    {
    }

    public string BankDetails { get; private set; } = string.Empty;

    public string EmailFromName { get; private set; } = string.Empty;

    public string? EmailReplyTo { get; private set; }

    public string? WhatsAppPhoneNumberId { get; private set; }

    public string? WhatsAppTokenEncrypted { get; private set; }

    public bool RemindersEnabled { get; private set; } = true;

    public int ReminderDaysBeforeDue { get; private set; } = 3;

    public bool ReminderOnDueDate { get; private set; } = true;

    public int ReminderRepeatEveryDays { get; private set; } = 7;

    public int ReminderMaxAfterDue { get; private set; } = 3;

    public static BusinessSettings Create(Guid tenantId, Guid businessId, DateTimeOffset now)
    {
        var settings = new BusinessSettings { TenantId = tenantId, BusinessId = businessId };
        settings.Initialise(now);
        return settings;
    }

    public void UpdateBankDetails(string bankDetails, DateTimeOffset now)
    {
        BankDetails = bankDetails.Trim();
        Touch(now);
    }

    public void UpdateMessaging(string emailFromName, string? emailReplyTo, string? whatsAppPhoneNumberId, string? newWhatsAppTokenEncrypted, DateTimeOffset now)
    {
        EmailFromName = emailFromName.Trim();
        EmailReplyTo = string.IsNullOrWhiteSpace(emailReplyTo) ? null : emailReplyTo.Trim();
        WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(whatsAppPhoneNumberId) ? null : whatsAppPhoneNumberId.Trim();
        if (!string.IsNullOrWhiteSpace(newWhatsAppTokenEncrypted))
        {
            WhatsAppTokenEncrypted = newWhatsAppTokenEncrypted;
        }

        Touch(now);
    }

    public void ClearWhatsAppToken(DateTimeOffset now)
    {
        WhatsAppTokenEncrypted = null;
        Touch(now);
    }

    public Result UpdateReminders(bool enabled, int daysBeforeDue, bool onDueDate, int repeatEveryDays, int maxAfterDue, DateTimeOffset now)
    {
        if (daysBeforeDue is < 0 or > 60 || repeatEveryDays is < 1 or > 60 || maxAfterDue is < 0 or > 10)
        {
            return Result.Failure(new Error("Reminders.Invalid", "Check the reminder days and the number of reminders."));
        }

        RemindersEnabled = enabled;
        ReminderDaysBeforeDue = daysBeforeDue;
        ReminderOnDueDate = onDueDate;
        ReminderRepeatEveryDays = repeatEveryDays;
        ReminderMaxAfterDue = maxAfterDue;
        Touch(now);
        return Result.Success();
    }
}
