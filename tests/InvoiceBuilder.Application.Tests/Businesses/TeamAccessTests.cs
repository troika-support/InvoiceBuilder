using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Businesses.Settings;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Businesses;

public sealed class TeamAccessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();

    private readonly ITeamStore _team = Substitute.For<ITeamStore>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();

    public TeamAccessTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _business.BusinessId.Returns(BusinessId);
    }

    private SetStaffAccessHandler Handler() => new(_tenant, _business, _team, _activity, TimeProvider.System);

    [Fact]
    public async Task R2_granting_staff_access_is_saved_and_audited()
    {
        _team.SetStaffAccessAsync(TenantId, BusinessId, "u1", true, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("Tendai");

        var result = await Handler().Handle(new SetStaffAccessCommand("u1", true), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _activity.Received(1).AddAsync("Business", BusinessId, "StaffAccessGranted", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R2_removing_staff_access_is_audited()
    {
        _team.SetStaffAccessAsync(TenantId, BusinessId, "u1", false, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("Tendai");

        var result = await Handler().Handle(new SetStaffAccessCommand("u1", false), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _activity.Received(1).AddAsync("Business", BusinessId, "StaffAccessRemoved", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R2_owner_or_unknown_user_cannot_be_changed_and_nothing_is_logged()
    {
        _team.SetStaffAccessAsync(TenantId, BusinessId, "owner", false, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await Handler().Handle(new SetStaffAccessCommand("owner", false), CancellationToken.None);

        result.Error.ShouldBe(TeamErrors.NotStaff);
        await _activity.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public async Task No_selected_business_fails()
    {
        _business.BusinessId.Returns((Guid?)null);

        var result = await Handler().Handle(new SetStaffAccessCommand("u1", true), CancellationToken.None);

        result.Error.ShouldBe(BusinessSettingsErrors.NoBusiness);
    }
}
