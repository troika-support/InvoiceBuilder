using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Businesses.Settings;

public sealed record TeamMember(string UserId, string Name, string Email, bool IsOwner, bool HasAccess, bool IsActive);

public sealed record BusinessAccessRow(Guid BusinessId, string Name, bool Assigned);

/// <summary>Users of the tenant and their assignment to businesses (R2). Implemented in Infrastructure because users live in Identity. Owners are never changed here.</summary>
public interface ITeamStore
{
    Task<IReadOnlyList<TeamMember>> ListAsync(Guid tenantId, Guid businessId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Grants or removes a Staff user's assignment. Returns the user's name, or null when the user is not a Staff member of this tenant.</summary>
    Task<string?> SetStaffAccessAsync(Guid tenantId, Guid businessId, string userId, bool grant, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Creates a Staff user assigned to one business. Returns the new user's id.</summary>
    Task<Result<string>> AddStaffAsync(Guid tenantId, string name, string email, string password, Guid businessId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Ids of the businesses a Staff user is assigned to, or null when the user is not a Staff member of this tenant.</summary>
    Task<IReadOnlyList<Guid>?> AssignedBusinessIdsAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Renames a Staff user and replaces their business assignments. Returns the user's name.</summary>
    Task<Result<string>> UpdateStaffAsync(Guid tenantId, string userId, string name, IReadOnlyList<Guid> businessIds, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Locks out (inactive) or re-enables a Staff user. History is kept. Returns the user's name.</summary>
    Task<Result<string>> SetActiveAsync(Guid tenantId, string userId, bool active, CancellationToken cancellationToken = default);

    Task<Result<string>> ResetPasswordAsync(Guid tenantId, string userId, string newPassword, CancellationToken cancellationToken = default);

    Task<Result<string>> ResetTwoFactorAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);
}

public static class TeamErrors
{
    public static readonly Error NotStaff = new("Team.NotStaff", "Only Staff members can be changed here.");

    public static readonly Error NoBusinesses = new("Team.NoBusinesses", "Give this person at least one business, or deactivate them instead.");
}

public sealed record GetTeamQuery : IRequest<Result<IReadOnlyList<TeamMember>>>;

public sealed class GetTeamHandler(ICurrentTenant tenant, ICurrentBusiness currentBusiness, ITeamStore team, TimeProvider timeProvider)
    : IRequestHandler<GetTeamQuery, Result<IReadOnlyList<TeamMember>>>
{
    public async Task<Result<IReadOnlyList<TeamMember>>> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<IReadOnlyList<TeamMember>>(BusinessSettingsErrors.NoBusiness);
        }

        return Result.Success(await team.ListAsync(tenant.TenantId, businessId, timeProvider.GetUtcNow(), cancellationToken));
    }
}

public sealed record GetTeamMemberAccessQuery(string UserId) : IRequest<Result<IReadOnlyList<BusinessAccessRow>>>;

public sealed class GetTeamMemberAccessHandler(ICurrentTenant tenant, IBusinessRepository businesses, ITeamStore team)
    : IRequestHandler<GetTeamMemberAccessQuery, Result<IReadOnlyList<BusinessAccessRow>>>
{
    public async Task<Result<IReadOnlyList<BusinessAccessRow>>> Handle(GetTeamMemberAccessQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await team.AssignedBusinessIdsAsync(tenant.TenantId, request.UserId, cancellationToken) is not { } assigned)
        {
            return Result.Failure<IReadOnlyList<BusinessAccessRow>>(TeamErrors.NotStaff);
        }

        var rows = (await businesses.ListAsync(cancellationToken))
            .OrderBy(b => b.TradingName)
            .Select(b => new BusinessAccessRow(b.Id, b.TradingName, assigned.Contains(b.Id)))
            .ToList();
        return Result.Success<IReadOnlyList<BusinessAccessRow>>(rows);
    }
}

public sealed record SetStaffAccessCommand(string UserId, bool Grant) : ITransactionalCommand<Result>;

public sealed class SetStaffAccessHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ITeamStore team,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<SetStaffAccessCommand, Result>
{
    public async Task<Result> Handle(SetStaffAccessCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var name = await team.SetStaffAccessAsync(tenant.TenantId, businessId, request.UserId, request.Grant, timeProvider.GetUtcNow(), cancellationToken);
        if (name is null)
        {
            return Result.Failure(TeamErrors.NotStaff);
        }

        await activity.AddAsync(
            nameof(Business),
            businessId,
            request.Grant ? "StaffAccessGranted" : "StaffAccessRemoved",
            request.Grant ? $"{name} was given access to this business." : $"{name} no longer has access to this business.",
            cancellationToken);
        return Result.Success();
    }
}

public sealed record AddTeamMemberCommand(string Name, string Email, string TemporaryPassword) : ITransactionalCommand<Result>;

public sealed class AddTeamMemberValidator : AbstractValidator<AddTeamMemberCommand>
{
    public AddTeamMemberValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Enter a name.").MaximumLength(100);
        RuleFor(c => c.Email).NotEmpty().WithMessage("Enter an email address.").EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(c => c.TemporaryPassword).NotEmpty().WithMessage("Enter a temporary password.").MinimumLength(12).WithMessage("Use at least 12 characters.");
    }
}

public sealed class AddTeamMemberHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ITeamStore team,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<AddTeamMemberCommand, Result>
{
    public async Task<Result> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var created = await team.AddStaffAsync(tenant.TenantId, request.Name.Trim(), request.Email.Trim(), request.TemporaryPassword, businessId, timeProvider.GetUtcNow(), cancellationToken);
        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        await activity.AddAsync(nameof(Business), businessId, "StaffAdded", $"{request.Name.Trim()} ({request.Email.Trim()}) was added as Staff with access to this business.", cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateTeamMemberCommand(string UserId, string Name, IReadOnlyList<Guid> BusinessIds) : ITransactionalCommand<Result>;

public sealed class UpdateTeamMemberValidator : AbstractValidator<UpdateTeamMemberCommand>
{
    public UpdateTeamMemberValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().WithMessage("Enter a name.").MaximumLength(100);
        RuleFor(c => c.BusinessIds).NotEmpty().WithMessage("Choose at least one business, or deactivate this person instead.");
    }
}

public sealed class UpdateTeamMemberHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    ITeamStore team,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<UpdateTeamMemberCommand, Result>
{
    public async Task<Result> Handle(UpdateTeamMemberCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var updated = await team.UpdateStaffAsync(tenant.TenantId, request.UserId, request.Name.Trim(), request.BusinessIds, timeProvider.GetUtcNow(), cancellationToken);
        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        await activity.AddAsync(nameof(Business), businessId, "StaffUpdated", $"{updated.Value} was updated; assigned to {request.BusinessIds.Count} business(es).", cancellationToken);
        return Result.Success();
    }
}

public sealed record SetTeamMemberActiveCommand(string UserId, bool Active) : ITransactionalCommand<Result>;

public sealed class SetTeamMemberActiveHandler(ICurrentTenant tenant, ICurrentBusiness currentBusiness, ITeamStore team, IActivityLog activity)
    : IRequestHandler<SetTeamMemberActiveCommand, Result>
{
    public async Task<Result> Handle(SetTeamMemberActiveCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var changed = await team.SetActiveAsync(tenant.TenantId, request.UserId, request.Active, cancellationToken);
        if (changed.IsFailure)
        {
            return Result.Failure(changed.Error);
        }

        await activity.AddAsync(nameof(Business), businessId, request.Active ? "StaffReactivated" : "StaffDeactivated", request.Active ? $"{changed.Value} was reactivated." : $"{changed.Value} was deactivated and can no longer sign in.", cancellationToken);
        return Result.Success();
    }
}

public sealed record ResetTeamMemberPasswordCommand(string UserId, string NewPassword) : ITransactionalCommand<Result>;

public sealed class ResetTeamMemberPasswordValidator : AbstractValidator<ResetTeamMemberPasswordCommand>
{
    public ResetTeamMemberPasswordValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().WithMessage("Enter a new password.").MinimumLength(12).WithMessage("Use at least 12 characters.");
    }
}

public sealed class ResetTeamMemberPasswordHandler(ICurrentTenant tenant, ICurrentBusiness currentBusiness, ITeamStore team, IActivityLog activity)
    : IRequestHandler<ResetTeamMemberPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetTeamMemberPasswordCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var reset = await team.ResetPasswordAsync(tenant.TenantId, request.UserId, request.NewPassword, cancellationToken);
        if (reset.IsFailure)
        {
            return Result.Failure(reset.Error);
        }

        await activity.AddAsync(nameof(Business), businessId, "StaffPasswordReset", $"Password was reset for {reset.Value}.", cancellationToken);
        return Result.Success();
    }
}

public sealed record ResetTeamMemberTwoFactorCommand(string UserId) : ITransactionalCommand<Result>;

public sealed class ResetTeamMemberTwoFactorHandler(ICurrentTenant tenant, ICurrentBusiness currentBusiness, ITeamStore team, IActivityLog activity)
    : IRequestHandler<ResetTeamMemberTwoFactorCommand, Result>
{
    public async Task<Result> Handle(ResetTeamMemberTwoFactorCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure(BusinessSettingsErrors.NoBusiness);
        }

        var reset = await team.ResetTwoFactorAsync(tenant.TenantId, request.UserId, cancellationToken);
        if (reset.IsFailure)
        {
            return Result.Failure(reset.Error);
        }

        await activity.AddAsync(nameof(Business), businessId, "StaffTwoFactorReset", $"Two-factor sign-in was reset for {reset.Value}.", cancellationToken);
        return Result.Success();
    }
}
