using System.Security.Claims;
using InvoiceBuilder.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBuilder.Integration.Tests.Security;

public sealed class AuthorizationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessA = Guid.NewGuid();
    private static readonly Guid BusinessB = Guid.NewGuid();

    private static IAuthorizationService Authz()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppAuthorization();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal User(string role, bool twoFactor, params Guid[] businesses)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new(AppClaims.TenantId, TenantId.ToString()),
        };
        claims.AddRange(businesses.Select(b => new Claim(AppClaims.BusinessId, b.ToString())));
        if (twoFactor)
        {
            claims.Add(new Claim(AppClaims.TwoFactorEnrolled, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task R2_staff_cannot_satisfy_the_owner_policy_used_by_settings()
    {
        var result = await Authz().AuthorizeAsync(User(Roles.Staff, true, BusinessA), Policies.Owner);
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task R2_owner_and_staff_satisfy_the_staff_policy()
    {
        (await Authz().AuthorizeAsync(User(Roles.Owner, true), Policies.Staff)).Succeeded.ShouldBeTrue();
        (await Authz().AuthorizeAsync(User(Roles.Staff, true, BusinessA), Policies.Staff)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task R2_user_assigned_to_business_a_is_forbidden_on_business_b()
    {
        var staff = User(Roles.Staff, true, BusinessA);
        (await Authz().AuthorizeAsync(staff, BusinessA, Policies.CanActOnBusiness)).Succeeded.ShouldBeTrue();
        (await Authz().AuthorizeAsync(staff, BusinessB, Policies.CanActOnBusiness)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task R2_owner_can_act_on_any_business_in_the_tenant()
    {
        (await Authz().AuthorizeAsync(User(Roles.Owner, true), BusinessB, Policies.CanActOnBusiness)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Owner_without_two_factor_is_forced_to_enrol()
    {
        TwoFactorGate.MustEnrol(User(Roles.Owner, false)).ShouldBeTrue();
        TwoFactorGate.MustEnrol(User(Roles.Owner, true)).ShouldBeFalse();
        TwoFactorGate.MustEnrol(User(Roles.Staff, false, BusinessA)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/account/2fa/setup", true)]
    [InlineData("/account/sign-out", true)]
    [InlineData("/_framework/blazor.web.js", true)]
    [InlineData("/", false)]
    [InlineData("/settings", false)]
    [InlineData("/app.css", true)]
    [InlineData("/fonts/manrope.woff2", true)]
    [InlineData("/invoices/x.y", false)]
    [InlineData("/settings/team.aspx", false)]
    public void Enrolment_gate_only_lets_enrolment_and_static_paths_through(string path, bool allowed) =>
        TwoFactorGate.IsAllowedWhileEnrolling(path).ShouldBe(allowed);

    private static ClaimsPrincipal WithSelected(ClaimsPrincipal user, Guid selected)
    {
        var identity = (ClaimsIdentity)user.Identity!;
        identity.AddClaim(new Claim(AppClaimsPrincipalFactory.SelectedBusinessClaim, selected.ToString()));
        return user;
    }

    [Fact]
    public void R2_staff_selection_outside_their_assignments_is_ignored()
    {
        var staff = WithSelected(User(Roles.Staff, false, BusinessA), BusinessB);
        WebCurrentScope.BusinessFromClaims(staff).ShouldBe(BusinessA);
    }

    [Fact]
    public void R2_staff_without_a_selection_never_get_the_whole_tenant()
    {
        WebCurrentScope.BusinessFromClaims(User(Roles.Staff, false, BusinessA)).ShouldBe(BusinessA);
        WebCurrentScope.BusinessFromClaims(User(Roles.Staff, false)).ShouldBe(Guid.Empty);
    }

    [Fact]
    public void R2_owner_keeps_their_selection_or_all_businesses()
    {
        WebCurrentScope.BusinessFromClaims(WithSelected(User(Roles.Owner, true), BusinessB)).ShouldBe(BusinessB);
        WebCurrentScope.BusinessFromClaims(User(Roles.Owner, true)).ShouldBeNull();
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public void R2_claims_factory_selection_rules(bool isOwner, bool preferAssigned, bool expectAssigned)
    {
        var preferred = preferAssigned ? BusinessA : BusinessB;
        var selected = AppClaimsPrincipalFactory.SelectFor(isOwner, preferred, [BusinessA]);
        selected.ShouldBe(expectAssigned ? BusinessA : BusinessB);
        AppClaimsPrincipalFactory.SelectFor(false, null, []).ShouldBe(Guid.Empty);
    }
}
