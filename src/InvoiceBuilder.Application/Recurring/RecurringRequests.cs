using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Recurring;
using MediatR;

namespace InvoiceBuilder.Application.Recurring;

public static class RecurringErrors
{
    public static readonly Error NotFound = new("Recurring.NotFound", "That recurring schedule no longer exists.");

    public static readonly Error NoBusiness = new("Recurring.NoBusiness", "Pick a business first.");

    public static readonly Error SourceInvalid = new("Recurring.SourceInvalid", "Only an issued invoice can be made recurring.");
}

public sealed record RecurringRow(
    Guid Id,
    Guid SourceDocumentId,
    string ClientName,
    string SourceNumber,
    RecurringCadence Cadence,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? MaxOccurrences,
    int Occurrences,
    bool AutoIssue,
    DateOnly NextRunDate,
    bool Paused);

public sealed record ListRecurringQuery : IRequest<IReadOnlyList<RecurringRow>>;

public sealed class ListRecurringHandler(IRecurringRepository schedules, IDocumentRepository documents, IClientRepository clients)
    : IRequestHandler<ListRecurringQuery, IReadOnlyList<RecurringRow>>
{
    public async Task<IReadOnlyList<RecurringRow>> Handle(ListRecurringQuery request, CancellationToken cancellationToken)
    {
        var rows = new List<RecurringRow>();
        foreach (var s in await schedules.ListAsync(cancellationToken))
        {
            var source = await documents.GetByIdAsync(s.SourceDocumentId, cancellationToken);
            var client = source is null ? null : await clients.GetByIdAsync(source.ClientId, cancellationToken);
            rows.Add(new RecurringRow(
                s.Id, s.SourceDocumentId, client?.Name ?? "—", source?.Number?.Value ?? "—",
                s.Cadence, s.StartDate, s.EndDate, s.MaxOccurrences, s.Occurrences, s.AutoIssue, s.NextRunDate, s.Paused));
        }

        return rows;
    }
}

public sealed record MakeRecurringCommand(
    Guid SourceDocumentId,
    RecurringCadence Cadence,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? MaxOccurrences,
    bool AutoIssue) : ITransactionalCommand<Result<Guid>>;

public sealed class MakeRecurringHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IRecurringRepository schedules,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<MakeRecurringCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(MakeRecurringCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<Guid>(RecurringErrors.NoBusiness);
        }

        var source = await documents.GetByIdAsync(request.SourceDocumentId, cancellationToken);
        if (source is null || source.BusinessId != businessId)
        {
            return Result.Failure<Guid>(RecurringErrors.SourceInvalid);
        }

        if (source.Type != DocumentType.Invoice || source.Number is null)
        {
            return Result.Failure<Guid>(RecurringErrors.SourceInvalid);
        }

        var now = timeProvider.GetUtcNow();
        var created = RecurringSchedule.Create(tenant.TenantId, businessId, source.Id, request.Cadence, request.StartDate, request.EndDate, request.MaxOccurrences, request.AutoIssue, now);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        schedules.Add(created.Value);
        await activity.AddAsync(nameof(RecurringSchedule), created.Value.Id, "RecurringCreated", $"{request.Cadence} schedule created from {source.Number.Value}.", cancellationToken);
        return Result.Success(created.Value.Id);
    }
}

public sealed record UpdateRecurringCommand(Guid Id, RecurringCadence Cadence, DateOnly? EndDate, int? MaxOccurrences, bool AutoIssue)
    : ITransactionalCommand<Result>;

public sealed class UpdateRecurringHandler(IRecurringRepository schedules, IActivityLog activity, TimeProvider timeProvider)
    : IRequestHandler<UpdateRecurringCommand, Result>
{
    public async Task<Result> Handle(UpdateRecurringCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var schedule = await schedules.GetByIdAsync(request.Id, cancellationToken);
        if (schedule is null)
        {
            return Result.Failure(RecurringErrors.NotFound);
        }

        var updated = schedule.Update(request.Cadence, request.EndDate, request.MaxOccurrences, request.AutoIssue, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated;
        }

        await activity.AddAsync(nameof(RecurringSchedule), schedule.Id, "RecurringUpdated", "Recurring schedule changed.", cancellationToken);
        return Result.Success();
    }
}

public sealed record SetRecurringPausedCommand(Guid Id, bool Paused) : ITransactionalCommand<Result>;

public sealed class SetRecurringPausedHandler(IRecurringRepository schedules, IActivityLog activity, TimeProvider timeProvider)
    : IRequestHandler<SetRecurringPausedCommand, Result>
{
    public async Task<Result> Handle(SetRecurringPausedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var schedule = await schedules.GetByIdAsync(request.Id, cancellationToken);
        if (schedule is null)
        {
            return Result.Failure(RecurringErrors.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        if (request.Paused)
        {
            schedule.Pause(now);
        }
        else
        {
            schedule.Resume(now);
        }

        await activity.AddAsync(nameof(RecurringSchedule), schedule.Id, request.Paused ? "RecurringPaused" : "RecurringResumed", request.Paused ? "Recurring schedule paused." : "Recurring schedule resumed.", cancellationToken);
        return Result.Success();
    }
}
