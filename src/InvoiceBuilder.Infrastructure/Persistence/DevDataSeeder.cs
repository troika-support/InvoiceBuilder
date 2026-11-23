using System.Text.Json;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Catalogue;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.Templates;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Development-only sample data (prompt 03 step 9). Idempotent: does nothing when the dev tenant exists.</summary>
public sealed partial class DevDataSeeder(AppDbContext db, TimeProvider timeProvider, ILogger<DevDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = DevCurrentTenant.DevTenantId;
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.Tenants.Add(Tenant.Create("Troika Design Workshop", now, tenantId));

        var business = Business.Create(
            tenantId,
            "Troika Design Workshop",
            "Troika Design Workshop (Pvt) Ltd",
            "hello@troika.co.zw",
            "Harare, Zimbabwe",
            now);
        db.Businesses.Add(business);

        var sequences = Enum.GetValues<DocumentType>()
            .Select(t => NumberSequence.CreateDefault(tenantId, business.Id, t, now))
            .ToDictionary(s => s.DocumentType);
        db.NumberSequences.AddRange(sequences.Values);

        var theme = Theme.Create(tenantId, business.Id, "Troika Amber", ThemeTokens, now).Value;
        db.Themes.Add(theme);
        Ensure(business.UpdateDefaults(CurrencyCode.Usd, 14, 14, true, 30m, theme.Id, now));

        var template = Template.Create(
            tenantId,
            business.Id,
            "Troika Classic",
            [DocumentType.Quote, DocumentType.Invoice, DocumentType.Receipt, DocumentType.CreditNote],
            theme.Id,
            now).Value;
        var templateVersion = template.PublishVersion(EmptyDefinition, Guid.Empty, now);
        db.Templates.Add(template);

        // VERIFY: the "canvas sample data" isn't in the repo; these clients and items are placeholders.
        var clients = new[]
        {
            ("Mopane Lodge", "accounts@mopanelodge.co.zw"),
            ("Baobab Coffee Roasters", "finance@baobabcoffee.co.zw"),
            ("Zambezi Outdoor", "billing@zambezioutdoor.co.zw"),
            ("Msasa Legal Practitioners", "admin@msasalegal.co.zw"),
            ("Kopje Bakery", "orders@kopjebakery.co.zw"),
        }.Select(c =>
        {
            var client = Client.Create(tenantId, business.Id, c.Item1, CurrencyCode.Usd, now).Value;
            client.UpdateContact(c.Item2, null, null, "Harare", null, now);
            return client;
        }).ToList();
        db.Clients.AddRange(clients);

        var items = new[]
        {
            ("Brand identity", 1200.00m, false),
            ("Logo design", 450.00m, false),
            ("Website design (5 pages)", 1800.00m, false),
            ("Social media pack", 350.00m, false),
            ("Print layout (per page)", 60.00m, false),
            ("Website care plan", 120.00m, true),
        };
        db.CatalogueItems.AddRange(items.Select(i =>
            CatalogueItem.Create(tenantId, business.Id, i.Item1, i.Item2, CurrencyCode.Usd, now, isRecurring: i.Item3).Value));

        // INV-0150 for $2,400.00 with a 3 × $800.00 monthly plan, first instalment paid.
        var client0 = clients[0];
        var invoice = Document.CreateDraft(tenantId, business.Id, DocumentType.Invoice, client0.Id, CurrencyCode.Usd, now);
        Ensure(invoice.AddLine(new LineInput("Brand identity", "Logo, palette, type and guidelines", 1m, 2400.00m), now));

        var issueDate = today.AddDays(-35);
        var plan = InstalmentPlanner.Generate(invoice.Total, 3, InstalmentFrequency.Monthly, issueDate).Value;
        Ensure(invoice.SetPlan(plan, InstalmentFrequency.Monthly, now));

        Ensure(sequences[DocumentType.Invoice].ContinueFrom(150, now));
        var number = sequences[DocumentType.Invoice].Allocate(now).Value;
        Ensure(invoice.Issue(
            new IssueRequest(
                number,
                issueDate,
                business.DefaultTermsDays,
                JsonSerializer.Serialize(new { name = client0.Name, email = client0.Email }),
                JsonSerializer.Serialize(new { tradingName = business.TradingName, email = business.Email }),
                templateVersion.Id),
            now));
        Ensure(invoice.MarkSent(now));
        db.Documents.Add(invoice);

        var payment = Payment.Create(
            tenantId,
            business.Id,
            client0.Id,
            800.00m,
            CurrencyCode.Usd,
            PaymentMethod.BankTransfer,
            "SEED-0001",
            now.AddDays(-30),
            now).Value;
        Ensure(payment.Confirm(now));
        Ensure(PaymentAllocator.Allocate(
            payment,
            [new AllocationRequest(invoice.Id, client0.Id, CurrencyCode.Usd, invoice.Total, 800.00m)]));
        payment.ClearDomainEvents();
        db.Payments.Add(payment);
        Ensure(invoice.ApplyPaymentStatus(0m, 800.00m, now));
        invoice.ClearDomainEvents();

        await db.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, business.Id);
    }

    private const string ThemeTokens =
        """{"ink":"#111111","accent":"#F59E0B","paper":"#FFFFFF","muted":"#6B6B6B","clientMuted":"#44403C","clientSurface":"#F5F5F4","fonts":{"body":"Manrope","mono":"IBM Plex Mono"}}""";

    // VERIFY: placeholder definition until the designer (template-rendering.md) produces real ones.
    private const string EmptyDefinition =
        """{"page":{"size":"A4"},"themeTokens":["ink","accent","paper","muted"],"elements":[]}""";

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seed failed: {result.Error.Code} {result.Error.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded development data for business {BusinessId}")]
    private static partial void LogSeeded(ILogger logger, Guid businessId);
}
