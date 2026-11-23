using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Businesses.Settings;
using InvoiceBuilder.Domain.Common;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Businesses;

public sealed class TeamManagementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();

    private readonly ITeamStore _team = Substitute.For<ITeamStore>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();

    public TeamManagementTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _business.BusinessId.Returns(BusinessId);
    }

    [Fact]
    public async Task R33_adding_a_team_member_is_audited_without_the_password()
    {
        _team.AddStaffAsync(TenantId, "Tendai", "t@x.co", "temporary-pass-1", BusinessId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Result.Success("u1"));
        string? summary = null;
        await _activity.AddAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Do<string>(s => summary = s), Arg.Any<CancellationToken>());

        var result = await new AddTeamMemberHandler(_tenant, _business, _team, _activity, TimeProvider.System)
            .Handle(new AddTeamMemberCommand(" Tendai ", " t@x.co ", "temporary-pass-1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        summary.ShouldNotBeNull();
        summary.ShouldNotContain("temporary-pass-1");
    }

    [Fact]
    public async Task Add_failure_from_identity_is_returned_and_not_audited()
    {
        _team.AddStaffAsync(default, default!, default!, default!, default, default, default).ReturnsForAnyArgs(Result.Failure<string>(new Error("Team.IdentityFailed", "Email taken.")));

        var result = await new AddTeamMemberHandler(_tenant, _business, _team, _activity, TimeProvider.System)
            .Handle(new AddTeamMemberCommand("Tendai", "t@x.co", "temporary-pass-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _activity.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public void Add_validator_rejects_blank_name_bad_email_and_short_password()
    {
        new AddTeamMemberValidator().Validate(new AddTeamMemberCommand("", "nope", "short")).Errors.Select(e => e.PropertyName)
            .Distinct().ShouldBe(["Name", "Email", "TemporaryPassword"], ignoreOrder: true);
    }

    [Fact]
    public async Task R2_update_replaces_assignments_and_is_audited()
    {
        var ids = new List<Guid> { BusinessId, Guid.NewGuid() };
        _team.UpdateStaffAsync(TenantId, "u1", "Tendai", ids, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(Result.Success("Tendai"));

        var result = await new UpdateTeamMemberHandler(_tenant, _business, _team, _activity, TimeProvider.System)
            .Handle(new UpdateTeamMemberCommand("u1", "Tendai", ids), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _activity.Received(1).AddAsync("Business", BusinessId, "StaffUpdated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, "StaffReactivated")]
    [InlineData(false, "StaffDeactivated")]
    public async Task R33_activation_changes_are_audited(bool active, string action)
    {
        _team.SetActiveAsync(TenantId, "u1", active, Arg.Any<CancellationToken>()).Returns(Result.Success("Tendai"));

        var result = await new SetTeamMemberActiveHandler(_tenant, _business, _team, _activity)
            .Handle(new SetTeamMemberActiveCommand("u1", active), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _activity.Received(1).AddAsync("Business", BusinessId, action, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_cannot_be_deactivated_or_reset()
    {
        _team.SetActiveAsync(TenantId, "owner", false, Arg.Any<CancellationToken>()).Returns(Result.Failure<string>(TeamErrors.NotStaff));
        _team.ResetPasswordAsync(TenantId, "owner", "a-new-password-1", Arg.Any<CancellationToken>()).Returns(Result.Failure<string>(TeamErrors.NotStaff));
        _team.ResetTwoFactorAsync(TenantId, "owner", Arg.Any<CancellationToken>()).Returns(Result.Failure<string>(TeamErrors.NotStaff));

        (await new SetTeamMemberActiveHandler(_tenant, _business, _team, _activity).Handle(new SetTeamMemberActiveCommand("owner", false), CancellationToken.None)).Error.ShouldBe(TeamErrors.NotStaff);
        (await new ResetTeamMemberPasswordHandler(_tenant, _business, _team, _activity).Handle(new ResetTeamMemberPasswordCommand("owner", "a-new-password-1"), CancellationToken.None)).Error.ShouldBe(TeamErrors.NotStaff);
        (await new ResetTeamMemberTwoFactorHandler(_tenant, _business, _team, _activity).Handle(new ResetTeamMemberTwoFactorCommand("owner"), CancellationToken.None)).Error.ShouldBe(TeamErrors.NotStaff);
        await _activity.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public async Task R33_password_and_2fa_resets_are_audited_without_the_password()
    {
        _team.ResetPasswordAsync(TenantId, "u1", "a-new-password-1", Arg.Any<CancellationToken>()).Returns(Result.Success("Tendai"));
        _team.ResetTwoFactorAsync(TenantId, "u1", Arg.Any<CancellationToken>()).Returns(Result.Success("Tendai"));
        string? summary = null;
        await _activity.AddAsync(Arg.Any<string>(), Arg.Any<Guid>(), "StaffPasswordReset", Arg.Do<string>(s => summary = s), Arg.Any<CancellationToken>());

        await new ResetTeamMemberPasswordHandler(_tenant, _business, _team, _activity).Handle(new ResetTeamMemberPasswordCommand("u1", "a-new-password-1"), CancellationToken.None);
        await new ResetTeamMemberTwoFactorHandler(_tenant, _business, _team, _activity).Handle(new ResetTeamMemberTwoFactorCommand("u1"), CancellationToken.None);

        summary.ShouldNotBeNull();
        summary.ShouldNotContain("a-new-password-1");
        await _activity.Received(1).AddAsync("Business", BusinessId, "StaffTwoFactorReset", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
