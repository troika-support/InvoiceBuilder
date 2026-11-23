using System.ComponentModel.DataAnnotations;

namespace InvoiceBuilder.Application.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"LogOnly" (default, Development) or "SendGrid".</summary>
    [Required]
    public string Provider { get; set; } = "LogOnly";

    /// <summary>Secret. user-secrets in dev, app-pool environment variable in production.</summary>
    public string? ApiKey { get; set; }

    [EmailAddress]
    public string? From { get; set; }

    public string? FromName { get; set; }
}

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>"LogOnly" (default, Development) or "Cloud".</summary>
    [Required]
    public string Provider { get; set; } = "LogOnly";

    /// <summary>Secret. VERIFY: spec section 9 wants this per business, encrypted; this is the single-account setting until then.</summary>
    public string? AccessToken { get; set; }

    public string? PhoneNumberId { get; set; }

    /// <summary>Meta app secret used to verify X-Hub-Signature-256 on the webhook.</summary>
    public string? AppSecret { get; set; }

    /// <summary>Token Meta echoes back when the webhook is first verified.</summary>
    public string? VerifyToken { get; set; }

    public string GraphApiVersion { get; set; } = "v21.0";

    public string DocumentLinkTemplate { get; set; } = "document_link";

    public string PaymentReceiptTemplate { get; set; } = "payment_receipt";

    public string TemplateLanguage { get; set; } = "en";
}
