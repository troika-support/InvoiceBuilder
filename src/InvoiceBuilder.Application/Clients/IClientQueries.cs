namespace InvoiceBuilder.Application.Clients;

public enum ClientFilter
{
    All = 0,
    WithBalanceDue = 1,
    Overdue = 2,
}

public sealed record ClientListItem(
    Guid Id,
    string Name,
    string? Email,
    DateOnly? LastDocumentDate,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyDictionary<string, decimal> BilledThisYear,
    string? Phone = null);

public sealed record ClientPage(IReadOnlyList<ClientListItem> Items, int TotalCount, int AllCount);

public sealed record ClientDocumentRow(Guid Id, string? Number, string Type, string Status, string Currency, decimal Total, DateOnly? IssueDate);

public sealed record ClientActivityRow(DateTimeOffset OccurredAt, string Summary);

public sealed record ClientDetail(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? WhatsApp,
    string? Address,
    string DefaultCurrency,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyDictionary<string, decimal> Overdue,
    IReadOnlyDictionary<string, decimal> BilledThisYear,
    double? AverageDaysToPay,
    IReadOnlyList<ClientDocumentRow> RecentDocuments,
    IReadOnlyList<ClientActivityRow> Activity);

/// <summary>Read model for the Clients screen. Implemented in Infrastructure so Application never touches the DbContext.</summary>
public interface IClientQueries
{
    Task<ClientPage> ListAsync(string? search, ClientFilter filter, int page, int pageSize, DateOnly today, CancellationToken cancellationToken = default);

    Task<ClientDetail?> GetDetailAsync(Guid clientId, DateOnly today, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? exceptClientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken = default);
}
