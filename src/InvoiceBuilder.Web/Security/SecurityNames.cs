namespace InvoiceBuilder.Web.Security;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
}

public static class Policies
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
    public const string CanActOnBusiness = "CanActOnBusiness";
}

public static class AppClaims
{
    public const string TenantId = "ib:tenant";
    public const string BusinessId = "ib:business";
    public const string TwoFactorEnrolled = "ib:2fa-enrolled";
}

/// <summary>Rate limit for the sign-in, verify, reset and setup pages: 10 form posts a minute per IP; page loads are not limited.</summary>
public static class AuthRateLimit
{
    public const string Policy = "auth-pages";
}
