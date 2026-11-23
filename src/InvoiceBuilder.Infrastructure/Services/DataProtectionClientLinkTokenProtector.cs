using System.Security.Cryptography;
using InvoiceBuilder.Application.Documents;
using Microsoft.AspNetCore.DataProtection;

namespace InvoiceBuilder.Infrastructure.Services;

public sealed class DataProtectionClientLinkTokenProtector(IDataProtectionProvider provider) : IClientLinkTokenProtector
{
    public const string Purpose = "InvoiceBuilder.ClientLinkToken.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string token) => _protector.Protect(token);

    public string? Unprotect(string? protectedToken)
    {
        if (string.IsNullOrEmpty(protectedToken))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
