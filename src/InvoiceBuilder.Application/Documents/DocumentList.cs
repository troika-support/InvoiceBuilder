using System.Globalization;
using System.Text;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

public enum DocumentListSort
{
    Issued = 0,
    Number = 1,
    Client = 2,
    Amount = 3,
    Status = 4,
}

public sealed record DocumentListFilter(
    IReadOnlyCollection<DocumentType> Types,
    IReadOnlyCollection<DocumentStatus> Statuses,
    bool OverdueOnly,
    Guid? ClientId,
    DateOnly? From,
    DateOnly? To,
    string? Currency,
    string? Search,
    DocumentListSort Sort = DocumentListSort.Issued,
    bool Descending = true);

/// <summary>One row of a documents list. <see cref="DaysLate"/> is set only when the invoice is overdue (R4).</summary>
public sealed record DocumentListRow(
    Guid Id,
    DocumentType Type,
    string? Number,
    string ClientName,
    DateOnly? IssueDate,
    DateOnly? DueOrValidUntil,
    string Currency,
    decimal Total,
    decimal? Balance,
    DocumentStatus Status,
    int? DaysLate,
    Guid ClientId = default);

public sealed record DocumentListPage(IReadOnlyList<DocumentListRow> Items, int TotalCount, int AllCount);

/// <summary>Read model for the documents lists. Implemented in Infrastructure so Application never touches the DbContext.</summary>
public interface IDocumentListQueries
{
    Task<DocumentListPage> ListAsync(DocumentListFilter filter, int page, int pageSize, DateOnly today, CancellationToken cancellationToken = default);
}

public sealed record GetDocumentsQuery(DocumentListFilter Filter, int Page = 1, int PageSize = 25) : IRequest<Result<DocumentListPage>>;

public sealed class GetDocumentsHandler(ICurrentBusiness currentBusiness, IDocumentListQueries queries, TimeProvider timeProvider)
    : IRequestHandler<GetDocumentsQuery, Result<DocumentListPage>>
{
    public async Task<Result<DocumentListPage>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is null)
        {
            return Result.Failure<DocumentListPage>(new Error("Documents.NoBusiness", "Pick a business first."));
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return Result.Success(await queries.ListAsync(request.Filter, request.Page, request.PageSize, today, cancellationToken));
    }
}

/// <summary>Builds the CSV for "Export CSV" from the rows of the filtered list. Amounts stay per currency (R12).</summary>
public static class DocumentListCsv
{
    private const string Header = "Number,Client,Type,Issued,Due or valid until,Currency,Total,Balance,Status";

    public static string Build(IEnumerable<DocumentListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder(Header).Append("\r\n");
        foreach (var r in rows)
        {
            sb.AppendJoin(',',
                    Escape(r.Number),
                    Escape(r.ClientName),
                    Escape(r.Type.ToString()),
                    Escape(r.IssueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Escape(r.DueOrValidUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Escape(r.Currency),
                    r.Total.ToString("0.00", CultureInfo.InvariantCulture),
                    r.Balance?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                    Escape(r.DaysLate is { } d ? $"Overdue ({d} days late)" : r.Status.ToString()))
                .Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Quotes fields and neutralises spreadsheet formula injection (leading = + - @).</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
