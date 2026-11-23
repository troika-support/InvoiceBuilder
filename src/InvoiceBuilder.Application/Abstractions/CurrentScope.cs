namespace InvoiceBuilder.Application.Abstractions;

/// <summary>The tenant of the signed-in user (R1). Implemented in Web later; a fixed dev tenant is used until then.</summary>
public interface ICurrentTenant
{
    Guid TenantId { get; }
}

/// <summary>
/// The business the user is working in (R2). Null means no business is selected and queries
/// are scoped to the tenant only (owner-wide views such as the all-businesses dashboard).
/// </summary>
public interface ICurrentBusiness
{
    Guid? BusinessId { get; }
}
