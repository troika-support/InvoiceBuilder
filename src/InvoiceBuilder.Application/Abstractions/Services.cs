using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Application.Abstractions;

/// <summary>The signed-in user (or the dev stand-in until authentication exists).</summary>
public interface ICurrentUser
{
    string? UserId { get; }

    /// <summary>Name shown in the activity trail.</summary>
    string Label { get; }
}

/// <summary>Writes the append-only audit trail (R33). Entries are saved with the surrounding unit of work.</summary>
public interface IActivityLog
{
    Task AddAsync(string entityType, Guid entityId, string action, string summary, CancellationToken cancellationToken = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>Display formats from design-system §5.2. Dates, money and numbers are always shown through this.</summary>
public interface IDisplayFormatter
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Method name is mandated by prompt 04 and the theme kit.")]
    string Date(DateTimeOffset value);

    string ShortDate(DateTimeOffset value);

    /// <summary>"{Month yyyy}" used in screen subtitles (DS §8.6, §8.8).</summary>
    string MonthYear(DateTimeOffset value);

    string Time(DateTimeOffset value);

    string Money(decimal amount, string currency, bool showCurrency = true);

    string DaysLate(int days);
}

public interface IFileStore
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the key does not exist.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Queues a just-issued document for fiscalisation (R32). Phase 2 replaces the implementation.</summary>
public interface IFiscalisationService
{
    Task QueueAsync(Guid documentId, CancellationToken cancellationToken = default);
}
