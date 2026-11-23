using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Businesses;

public enum UserBusinessRole
{
    Owner = 1,
    Staff = 2,
}

/// <summary>Assigns a user to a business with a role (R2). Tenant-scoped so a user's other assignments stay visible while one business is selected.</summary>
public sealed class UserBusiness : TenantEntity
{
    private UserBusiness()
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid BusinessId { get; private set; }

    public UserBusinessRole Role { get; private set; }

    public static UserBusiness Create(Guid tenantId, string userId, Guid businessId, UserBusinessRole role, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var assignment = new UserBusiness
        {
            TenantId = tenantId,
            UserId = userId,
            BusinessId = businessId,
            Role = role,
        };
        assignment.Initialise(now);
        return assignment;
    }
}
