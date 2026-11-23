using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Businesses;

/// <summary>The tenant itself: it has the base fields but no TenantId (R1).</summary>
public sealed class Tenant : Entity
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public static Tenant Create(string name, DateTimeOffset now, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var tenant = new Tenant { Name = name.Trim() };
        if (id is { } fixedId)
        {
            tenant.Id = fixedId;
        }

        tenant.Initialise(now);
        return tenant;
    }
}
