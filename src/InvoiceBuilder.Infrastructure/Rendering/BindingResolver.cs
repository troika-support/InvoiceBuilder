using System.Globalization;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Templates;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Rendering;

/// <summary>Resolves binding paths against the whitelist in template-rendering.md §3. Nothing else resolves, and nothing executes.</summary>
public sealed partial class BindingResolver(IDisplayFormatter formatter, ILogger<BindingResolver> logger)
{
    private static IReadOnlySet<string> Allowed => TemplateBindings.Allowed;

    public static IReadOnlySet<string> AllowedPaths => TemplateBindings.Allowed;

    /// <summary>Display text, not yet HTML-encoded. An unknown path gives an empty string and a warning.</summary>
    public string Resolve(string? path, DocumentRenderModel model, string? format = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (path is null || !Allowed.Contains(path))
        {
            LogUnknownBinding(path);
            return string.Empty;
        }

        return Format(Lookup(path, model), model, format);
    }

    /// <summary>Only <c>business.logo</c> is an image binding, and only a data: image URI is accepted.</summary>
    public static string? ResolveImage(string? path, DocumentRenderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return string.Equals(path, "business.logo", StringComparison.Ordinal)
               && model.Business.LogoDataUri is { } uri
               && uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            ? uri
            : null;
    }

    private static object? Lookup(string path, DocumentRenderModel m) => path switch
    {
        "business.tradingName" => m.Business.TradingName,
        "business.registeredName" => m.Business.RegisteredName,
        "business.email" => m.Business.Email,
        "business.phone" => m.Business.Phone,
        "business.address" => m.Business.Address,
        "business.bankDetails" => m.Business.BankDetails,
        "document.status" => m.Document.Status,
        "client.name" => m.Client.Name,
        "client.email" => m.Client.Email,
        "client.phone" => m.Client.Phone,
        "client.address" => m.Client.Address,
        "document.typeLabel" => m.Document.TypeLabel,
        "document.number" => m.Document.Number,
        "document.issueDate" => m.Document.IssueDate,
        "document.dueDate" => m.Document.DueDate,
        "document.validUntil" => m.Document.ValidUntil,
        "document.currency" => m.Document.Currency,
        "document.subtotal" => m.Document.Subtotal,
        "document.discountTotal" => m.Document.DiscountTotal,
        "document.taxTotal" => m.Document.TaxTotal,
        "document.total" => m.Document.Total,
        "document.amountPaid" => m.Document.AmountPaid,
        "document.balance" => m.Document.Balance,
        "document.notes" => m.Document.Notes,
        "document.terms" => m.Document.Terms,
        "document.clientLinkUrl" => m.Document.ClientLinkUrl,
        "plan.count" => m.Plan?.Count,
        "plan.nextInstalment.number" => m.Plan?.NextNumber,
        "plan.nextInstalment.amount" => m.Plan?.NextAmount,
        "plan.nextInstalment.dueDate" => m.Plan?.NextDueDate,
        "plan.overdueAmount" => m.Plan?.OverdueAmount,
        "acceptance.signerName" => m.Acceptance?.SignerName,
        "acceptance.acceptedAt" => m.Acceptance?.AcceptedAt,
        _ => null, // business.logo (image only) and page.* (not available inside the body).
    };

    private string Format(object? value, DocumentRenderModel model, string? format) => value switch
    {
        null => string.Empty,
        string text => text,
        decimal amount when string.Equals(format, "text", StringComparison.Ordinal) => amount.ToString("0.##", CultureInfo.InvariantCulture),
        decimal amount => formatter.Money(amount, model.Document.Currency),
        DateOnly date => formatter.Date(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)),
        DateTimeOffset moment => formatter.Date(moment),
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown binding path {Path}; rendered empty")]
    private partial void LogUnknownBinding(string? path);
}
