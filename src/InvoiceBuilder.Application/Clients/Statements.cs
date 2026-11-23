using System.Globalization;
using System.Text;
using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Options;
using InvoiceBuilder.Application.Templates;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;
using Microsoft.Extensions.Options;

namespace InvoiceBuilder.Application.Clients;

// ---------------------------------------------------------------------------------------------------------------
// Data
// ---------------------------------------------------------------------------------------------------------------

public enum StatementEntryKind
{
    Invoice = 0,
    CreditNote = 1,
    Payment = 2,
}

/// <summary>One movement on a client's account. Invoices are charges; credit notes and payments reduce what is owed.</summary>
public sealed record StatementEntry(DateOnly Date, StatementEntryKind Kind, string Currency, string Reference, string Details, decimal Amount);

public sealed record StatementClient(Guid Id, string Name, string? Email, string? Address);

/// <summary>Everything ever recorded for a client: issued invoices and credit notes, confirmed payments.</summary>
public sealed record StatementSource(StatementClient Client, IReadOnlyList<StatementEntry> Entries);

public interface IStatementQueries
{
    /// <summary>Null when the client isn't in the current business. Payment dates are in <paramref name="zone"/>.</summary>
    Task<StatementSource?> LoadAsync(Guid clientId, TimeZoneInfo zone, CancellationToken cancellationToken = default);
}

public sealed record StatementLine(DateOnly Date, StatementEntryKind Kind, string Reference, string Details, decimal Charge, decimal Credit, decimal Balance);

/// <summary>One currency's part of a statement (R12: never mixed). Closing = opening + invoiced − credited − paid.</summary>
public sealed record StatementSection(
    string Currency,
    decimal Opening,
    IReadOnlyList<StatementLine> Lines,
    decimal Invoiced,
    decimal Credited,
    decimal Paid,
    decimal Closing);

public sealed record StatementBusiness(string TradingName, string? RegisteredName, string Email, string? Phone, string Address, string? LogoDataUri, string? BankDetails);

public sealed record ClientStatement(
    StatementBusiness Business,
    StatementClient Client,
    DateOnly From,
    DateOnly To,
    DateOnly IssuedOn,
    IReadOnlyList<StatementSection> Sections,
    IReadOnlyDictionary<string, string> ThemeTokens);

/// <summary>Turns a statement into a printable, self-contained HTML page (fonts and logo inline, no scripts).</summary>
public interface IStatementRenderer
{
    string RenderHtml(ClientStatement statement);
}

// ---------------------------------------------------------------------------------------------------------------
// Rules
// ---------------------------------------------------------------------------------------------------------------

/// <summary>Spec §4.2 client statement: opening balance, the period's documents and payments, closing balance, per currency.</summary>
public static class StatementCalculator
{
    public static IReadOnlyList<StatementSection> Build(IEnumerable<StatementEntry> entries, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var sections = new List<StatementSection>();
        foreach (var currency in entries.Where(e => e.Date <= to).GroupBy(e => e.Currency).OrderBy(g => g.Key == "USD" ? 0 : 1).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var opening = currency.Where(e => e.Date < from).Sum(Signed);
            var balance = opening;
            var lines = new List<StatementLine>();

            // Same day: charges first, then credits, so the running balance reads naturally.
            foreach (var e in currency.Where(e => e.Date >= from).OrderBy(e => e.Date).ThenBy(e => e.Kind).ThenBy(e => e.Reference, StringComparer.Ordinal))
            {
                balance += Signed(e);
                lines.Add(new StatementLine(
                    e.Date,
                    e.Kind,
                    e.Reference,
                    e.Details,
                    e.Kind == StatementEntryKind.Invoice ? e.Amount : 0m,
                    e.Kind == StatementEntryKind.Invoice ? 0m : e.Amount,
                    balance));
            }

            if (opening == 0m && lines.Count == 0)
            {
                continue;
            }

            var inPeriod = currency.Where(e => e.Date >= from).ToList();
            sections.Add(new StatementSection(
                currency.Key,
                opening,
                lines,
                inPeriod.Where(e => e.Kind == StatementEntryKind.Invoice).Sum(e => e.Amount),
                inPeriod.Where(e => e.Kind == StatementEntryKind.CreditNote).Sum(e => e.Amount),
                inPeriod.Where(e => e.Kind == StatementEntryKind.Payment).Sum(e => e.Amount),
                balance));
        }

        return sections;
    }

    private static decimal Signed(StatementEntry e) => e.Kind == StatementEntryKind.Invoice ? e.Amount : -e.Amount;
}

/// <summary>CSV of a statement: UTF-8 (the download adds the BOM), invariant numbers, ISO dates, one block per currency.</summary>
public static class StatementCsv
{
    public static string Build(ClientStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("Currency,Date,Type,Reference,Details,Charge,Payment or credit,Balance\r\n");
        foreach (var s in statement.Sections)
        {
            sb.AppendJoin(',', s.Currency, statement.From.ToString("yyyy-MM-dd", inv), "Opening balance", string.Empty, string.Empty, string.Empty, string.Empty, s.Opening.ToString("0.00", inv)).Append("\r\n");
            foreach (var l in s.Lines)
            {
                sb.AppendJoin(',',
                        s.Currency,
                        l.Date.ToString("yyyy-MM-dd", inv),
                        KindLabel(l.Kind),
                        DocumentListCsv.Escape(l.Reference),
                        DocumentListCsv.Escape(l.Details),
                        l.Charge == 0m ? string.Empty : l.Charge.ToString("0.00", inv),
                        l.Credit == 0m ? string.Empty : l.Credit.ToString("0.00", inv),
                        l.Balance.ToString("0.00", inv))
                    .Append("\r\n");
            }

            sb.AppendJoin(',', s.Currency, statement.To.ToString("yyyy-MM-dd", inv), "Closing balance", string.Empty, string.Empty, string.Empty, string.Empty, s.Closing.ToString("0.00", inv)).Append("\r\n");
        }

        return sb.ToString();
    }

    public static string KindLabel(StatementEntryKind kind) => kind switch
    {
        StatementEntryKind.Invoice => "Invoice",
        StatementEntryKind.CreditNote => "Credit note",
        _ => "Payment",
    };
}

// ---------------------------------------------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------------------------------------------

public static class StatementErrors
{
    public static readonly Error ClientNotFound = new("Statement.ClientNotFound", "That client could not be found.");

    public static readonly Error BadPeriod = new("Statement.BadPeriod", "The start date must be on or before the end date.");
}

/// <summary>A client's statement for a period, in every currency they have dealt in.</summary>
public sealed record GetClientStatementQuery(Guid ClientId, DateOnly From, DateOnly To) : IRequest<Result<ClientStatement>>;

public sealed class GetClientStatementValidator : AbstractValidator<GetClientStatementQuery>
{
    public GetClientStatementValidator()
    {
        RuleFor(q => q.ClientId).NotEmpty();
        RuleFor(q => q).Must(q => q.From <= q.To).WithMessage(StatementErrors.BadPeriod.Message);
        RuleFor(q => q).Must(q => q.To.DayNumber - q.From.DayNumber <= 3660).WithMessage("Choose a period of 10 years or less.");
    }
}

public sealed class GetClientStatementHandler(
    ICurrentBusiness currentBusiness,
    IStatementQueries statements,
    IBusinessRepository businesses,
    IBusinessSettingsRepository settings,
    ITemplateRepository templates,
    IOptions<AppOptions> app,
    TimeProvider time) : IRequestHandler<GetClientStatementQuery, Result<ClientStatement>>
{
    public async Task<Result<ClientStatement>> Handle(GetClientStatementQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId)
        {
            return Result.Failure<ClientStatement>(new Error("Statement.NoBusiness", "Pick a business first."));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(app.Value.TimeZone);
        var source = await statements.LoadAsync(request.ClientId, zone, cancellationToken);
        var business = await businesses.GetByIdAsync(businessId, cancellationToken);
        if (source is null || business is null)
        {
            return Result.Failure<ClientStatement>(StatementErrors.ClientNotFound);
        }

        var bank = (await settings.GetAsync(businessId, cancellationToken))?.BankDetails;
        var logo = business.Logo is { Length: > 0 } bytes && business.LogoContentType is { } type
            ? $"data:{type};base64,{Convert.ToBase64String(bytes)}"
            : null;

        // The statement wears the same theme as the business's default invoice template.
        var defaults = await templates.ListDefaultsAsync(cancellationToken);
        var invoiceTemplateId = defaults.FirstOrDefault(d => d.DocumentType == DocumentType.Invoice)?.TemplateId;
        var template = invoiceTemplateId is { } id ? await templates.GetByIdAsync(id, cancellationToken) : null;
        var theme = template is null ? null : await templates.GetThemeAsync(template.ThemeId, cancellationToken);

        return Result.Success(new ClientStatement(
            new StatementBusiness(
                business.TradingName,
                business.RegisteredName,
                business.Email,
                business.WhatsAppNumber,
                business.Address,
                logo,
                string.IsNullOrWhiteSpace(bank) ? null : bank),
            source.Client,
            request.From,
            request.To,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime),
            StatementCalculator.Build(source.Entries, request.From, request.To),
            DocumentHtmlService.ThemeTokens(theme?.Tokens)));
    }
}

public sealed record StatementFile(byte[] Content, string FileName, string ContentType);

/// <summary>The statement as a PDF (same Chromium renderer as documents) or CSV.</summary>
public sealed record GetClientStatementFileQuery(Guid ClientId, DateOnly From, DateOnly To, bool Csv = false) : IRequest<Result<StatementFile>>;

public sealed class GetClientStatementFileHandler(ISender sender, IStatementRenderer renderer, IPdfRenderer pdf)
    : IRequestHandler<GetClientStatementFileQuery, Result<StatementFile>>
{
    public async Task<Result<StatementFile>> Handle(GetClientStatementFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var statement = await sender.Send(new GetClientStatementQuery(request.ClientId, request.From, request.To), cancellationToken);
        if (statement.IsFailure)
        {
            return Result.Failure<StatementFile>(statement.Error);
        }

        var name = StatementFileName(statement.Value);
        if (request.Csv)
        {
            return Result.Success(new StatementFile(
                [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(StatementCsv.Build(statement.Value))],
                name + ".csv",
                "text/csv; charset=utf-8"));
        }

        var bytes = await pdf.RenderAsync(renderer.RenderHtml(statement.Value), cancellationToken);
        return Result.Success(new StatementFile(bytes, name + ".pdf", "application/pdf"));
    }

    public static string StatementFileName(ClientStatement s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var safe = new string(s.Client.Name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (safe.Contains("--", StringComparison.Ordinal))
        {
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        }

        return $"Statement-{(safe.Length == 0 ? "client" : safe)}-{s.From:yyyy-MM-dd}-to-{s.To:yyyy-MM-dd}";
    }
}

/// <summary>Queues the statement PDF to be emailed to the client ("Send statement" on the client panel).</summary>
public sealed record SendClientStatementCommand(Guid ClientId, DateOnly From, DateOnly To, string Email, string? Message) : IRequest<Result>;

public sealed class SendClientStatementValidator : AbstractValidator<SendClientStatementCommand>
{
    public SendClientStatementValidator()
    {
        RuleFor(c => c.ClientId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().WithMessage("Enter an email address.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(c => c.Message).MaximumLength(1000);
        RuleFor(c => c).Must(c => c.From <= c.To).WithMessage(StatementErrors.BadPeriod.Message);
    }
}

public interface IStatementJobScheduler
{
    void Enqueue(Guid clientId, DateOnly periodStart, DateOnly periodEnd, string email, string? message);
}

public sealed class SendClientStatementHandler(ISender sender, IStatementJobScheduler jobs, IActivityLog activity, IUnitOfWork unitOfWork)
    : IRequestHandler<SendClientStatementCommand, Result>
{
    public async Task<Result> Handle(SendClientStatementCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Build it once now so problems (wrong client, bad period) show in the dialog, not later in a job.
        var statement = await sender.Send(new GetClientStatementQuery(request.ClientId, request.From, request.To), cancellationToken);
        if (statement.IsFailure)
        {
            return Result.Failure(statement.Error);
        }

        await activity.AddAsync("Client", request.ClientId, "StatementQueued", $"Statement for {Period(request.From, request.To)} queued to {request.Email.Trim()}.", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        jobs.Enqueue(request.ClientId, request.From, request.To, request.Email.Trim(), string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim());
        return Result.Success();
    }

    public static string Period(DateOnly from, DateOnly to) =>
        $"{from.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {to.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
}
