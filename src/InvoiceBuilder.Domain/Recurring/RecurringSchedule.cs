using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Recurring;

// VERIFY: spec §6 lists "Cadence" without values; these are assumed.
public enum RecurringCadence
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3,
}

public sealed class RecurringSchedule : BusinessEntity
{
    private RecurringSchedule()
    {
    }

    public Guid SourceDocumentId { get; private set; }

    public RecurringCadence Cadence { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public int? MaxOccurrences { get; private set; }

    public bool AutoIssue { get; private set; }

    public DateOnly NextRunDate { get; private set; }

    public bool Paused { get; private set; }

    public int Occurrences { get; private set; }

    public bool IsFinished =>
        (MaxOccurrences is { } max && Occurrences >= max) || (EndDate is { } end && NextRunDate > end);

    public static Result<RecurringSchedule> Create(
        Guid tenantId,
        Guid businessId,
        Guid sourceDocumentId,
        RecurringCadence cadence,
        DateOnly startDate,
        DateOnly? endDate,
        int? maxOccurrences,
        bool autoIssue,
        DateTimeOffset now)
    {
        if (endDate is { } end && end < startDate)
        {
            return Result.Failure<RecurringSchedule>(new Error("Recurring.EndBeforeStart", "The end date cannot be before the start date."));
        }

        if (maxOccurrences is <= 0)
        {
            return Result.Failure<RecurringSchedule>(new Error("Recurring.MaxInvalid", "Maximum occurrences must be above zero."));
        }

        var schedule = new RecurringSchedule
        {
            TenantId = tenantId,
            BusinessId = businessId,
            SourceDocumentId = sourceDocumentId,
            Cadence = cadence,
            StartDate = startDate,
            EndDate = endDate,
            MaxOccurrences = maxOccurrences,
            AutoIssue = autoIssue,
            NextRunDate = startDate,
        };
        schedule.Initialise(now);
        return Result.Success(schedule);
    }

    public void Pause(DateTimeOffset now)
    {
        Paused = true;
        Touch(now);
    }

    public void Resume(DateTimeOffset now)
    {
        Paused = false;
        Touch(now);
    }

    public Result Update(RecurringCadence cadence, DateOnly? endDate, int? maxOccurrences, bool autoIssue, DateTimeOffset now)
    {
        if (endDate is { } end && end < StartDate)
        {
            return Result.Failure(new Error("Recurring.EndBeforeStart", "The end date cannot be before the start date."));
        }

        if (maxOccurrences is <= 0)
        {
            return Result.Failure(new Error("Recurring.MaxInvalid", "Maximum occurrences must be above zero."));
        }

        Cadence = cadence;
        EndDate = endDate;
        MaxOccurrences = maxOccurrences;
        AutoIssue = autoIssue;
        NextRunDate = ComputeNextRunDate();
        Touch(now);
        return Result.Success();
    }

    public void RecordRun(DateTimeOffset now)
    {
        Occurrences++;
        NextRunDate = ComputeNextRunDate();
        Touch(now);
    }

    private DateOnly ComputeNextRunDate() => Cadence switch
    {
        RecurringCadence.Weekly => StartDate.AddDays(7 * Occurrences),
        RecurringCadence.Monthly => StartDate.AddMonths(Occurrences),
        RecurringCadence.Quarterly => StartDate.AddMonths(3 * Occurrences),
        _ => StartDate.AddYears(Occurrences),
    };
}
