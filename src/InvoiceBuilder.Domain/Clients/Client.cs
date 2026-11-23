using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Clients;

public sealed class Client : BusinessEntity
{
    private Client()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? WhatsApp { get; private set; }

    public string? Address { get; private set; }

    public CurrencyCode DefaultCurrency { get; private set; } = CurrencyCode.Usd;

    public string? Notes { get; private set; }

    public bool IsArchived { get; private set; }

    public static Result<Client> Create(Guid tenantId, Guid businessId, string name, CurrencyCode defaultCurrency, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Client>(new Error("Client.NameRequired", "A client needs a name."));
        }

        var client = new Client { TenantId = tenantId, BusinessId = businessId, Name = name.Trim(), DefaultCurrency = defaultCurrency };
        client.Initialise(now);
        return Result.Success(client);
    }

    public void UpdateContact(string? email, string? phone, string? whatsApp, string? address, string? notes, DateTimeOffset now)
    {
        Email = email;
        Phone = phone;
        WhatsApp = whatsApp;
        Address = address;
        Notes = notes;
        Touch(now);
    }

    public Result Update(string name, CurrencyCode defaultCurrency, string? email, string? phone, string? whatsApp, string? address, string? notes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("Client.NameRequired", "A client needs a name."));
        }

        Name = name.Trim();
        DefaultCurrency = defaultCurrency;
        UpdateContact(email, phone, whatsApp, address, notes, now);
        return Result.Success();
    }

    /// <summary>Archived clients are hidden from lists but keep their documents and history.</summary>
    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        Touch(now);
    }
}
