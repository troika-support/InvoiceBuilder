using InvoiceBuilder.Application.Abstractions;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Fixed tenant used until authentication supplies the real one.</summary>
public sealed class DevCurrentTenant : ICurrentTenant
{
    public static readonly Guid DevTenantId = new("0198a000-0000-7000-8000-000000000001");

    public Guid TenantId => DevTenantId;
}

/// <summary>Stand-in user until authentication (prompt 08) supplies the signed-in one.</summary>
public sealed class DevCurrentUser : ICurrentUser
{
    public string? UserId => null;

    public string Label => "Owner (dev)";
}

/// <summary>No business selected
public sealed class DevCurrentBusiness : ICurrentBusiness
{
    public Guid? BusinessId => null;
}
