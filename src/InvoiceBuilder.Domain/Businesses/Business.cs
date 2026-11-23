using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Businesses;

public sealed class Business : TenantEntity
{
    private Business()
    {
    }

    public string TradingName { get; private set; } = string.Empty;

    public string RegisteredName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? WhatsAppNumber { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public Guid? LogoAssetId { get; private set; }

    public const int MaxLogoBytes = 1024 * 1024;

    /// <summary>Logo image (PNG or SVG, at most 1 MB) shown on templates and the business switcher.</summary>
    public byte[]? Logo { get; private set; }

    public string? LogoContentType { get; private set; }

    public CurrencyCode DefaultCurrency { get; private set; } = CurrencyCode.Usd;

    public int DefaultTermsDays { get; private set; } = 14;

    public Guid? DefaultThemeId { get; private set; }

    public int QuoteValidityDays { get; private set; } = 14;

    public bool AllowPartPayments { get; private set; } = true;

    /// <summary>Minimum online part payment as a percentage of the balance (R20); default 30 per the owner.</summary>
    public decimal MinPartPaymentPercent { get; private set; } = 30m;

    public static Business Create(
        Guid tenantId,
        string tradingName,
        string registeredName,
        string email,
        string address,
        DateTimeOffset now,
        string? whatsAppNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tradingName);

        var business = new Business
        {
            TenantId = tenantId,
            TradingName = tradingName.Trim(),
            RegisteredName = registeredName,
            Email = email,
            Address = address,
            WhatsAppNumber = whatsAppNumber,
        };
        business.Initialise(now);
        return business;
    }

    public Result UpdateDetails(
        string tradingName,
        string registeredName,
        string email,
        string? whatsAppNumber,
        string address,
        Guid? logoAssetId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tradingName))
        {
            return Result.Failure(new Error("Business.TradingNameRequired", "A trading name is required."));
        }

        TradingName = tradingName.Trim();
        RegisteredName = registeredName;
        Email = email;
        WhatsAppNumber = whatsAppNumber;
        Address = address;
        LogoAssetId = logoAssetId;
        Touch(now);
        return Result.Success();
    }

    public Result SetLogo(byte[] content, string contentType, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (contentType is not ("image/png" or "image/svg+xml"))
        {
            return Result.Failure(new Error("Business.LogoType", "Upload a PNG or SVG logo."));
        }

        if (content.Length is 0 or > MaxLogoBytes)
        {
            return Result.Failure(new Error("Business.LogoSize", "The logo must be 1 MB or smaller."));
        }

        Logo = content;
        LogoContentType = contentType;
        Touch(now);
        return Result.Success();
    }

    public void RemoveLogo(DateTimeOffset now)
    {
        Logo = null;
        LogoContentType = null;
        Touch(now);
    }

    public Result UpdateDefaults(
        CurrencyCode defaultCurrency,
        int defaultTermsDays,
        int quoteValidityDays,
        bool allowPartPayments,
        decimal minPartPaymentPercent,
        Guid? defaultThemeId,
        DateTimeOffset now)
    {
        if (defaultTermsDays < 0 || quoteValidityDays < 1)
        {
            return Result.Failure(new Error("Business.InvalidDefaults", "Terms cannot be negative and quote validity must be at least 1 day."));
        }

        if (minPartPaymentPercent is < 0m or > 100m)
        {
            return Result.Failure(new Error("Business.InvalidDefaults", "The minimum part payment must be between 0 and 100 percent."));
        }

        DefaultCurrency = defaultCurrency;
        DefaultTermsDays = defaultTermsDays;
        QuoteValidityDays = quoteValidityDays;
        AllowPartPayments = allowPartPayments;
        MinPartPaymentPercent = minPartPaymentPercent;
        DefaultThemeId = defaultThemeId;
        Touch(now);
        return Result.Success();
    }
}
