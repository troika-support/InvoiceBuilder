using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Activity;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Adds entries to the current unit of work; they are saved with it, so an action and its audit line commit together.</summary>
public sealed class ActivityLog(
    AppDbContext db,
    ICurrentTenant tenant,
    ICurrentBusiness business,
    ICurrentUser user,
    TimeProvider timeProvider) : IActivityLog
{
    public Task AddAsync(string entityType, Guid entityId, string action, string summary, CancellationToken cancellationToken = default)
    {
        db.ActivityEntries.Add(ActivityEntry.Create(
            tenant.TenantId,
            business.BusinessId,
            entityType,
            entityId,
            action,
            user.UserId,
            user.Label,
            summary,
            timeProvider.GetUtcNow()));
        return Task.CompletedTask;
    }
}
