using InvoiceBuilder.Application.Payments;
using Microsoft.AspNetCore.DataProtection;

namespace InvoiceBuilder.Infrastructure.Payments;

public sealed class DataProtectionBusinessSecretStore(IDataProtectionProvider provider) : IBusinessSecretStore
{
    public const string Purpose = "Paynow.IntegrationKey";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
