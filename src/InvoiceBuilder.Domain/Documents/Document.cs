using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Payments;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Documents;

/// <summary>Everything that is locked when a document is issued (R5, R6).</summary>
public sealed record IssueRequest(
    DocumentNumber Number,
    DateOnly IssueDate,
    int TermsDays,
    string ClientSnapshot,
    string BusinessSnapshot,
    Guid TemplateVersionId,
    decimal? ExchangeRate = null,
    int QuoteValidityDays = 14,
    string? LinkTokenHash = null,
    string? LinkTokenProtected = null);

/// <summary>Quote, invoice, receipt or credit note (R3). Editable only while Draft (R5).</summary>
public sealed class Document : BusinessEntity
{
    private readonly List<DocumentLine> _lines = [];
    private readonly List<Instalment> _instalments = [];

    private Document()
    {
    }

    public DocumentType Type { get; private set; }

    public DocumentNumber? Number { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public Guid ClientId { get; private set; }

    public string ClientSnapshot { get; private set; } = "{}";

    public string BusinessSnapshot { get; private set; } = "{}";

    public CurrencyCode Currency { get; private set; } = CurrencyCode.Usd;

    /// <summary>ZiG per USD, entered manually at issue; for reporting only (R11).</summary>
    public decimal? ExchangeRate { get; private set; }

    public DateOnly? IssueDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public Discount Discount { get; private set; } = Discount.None;

    public decimal Subtotal { get; private set; }

    public decimal DiscountTotal { get; private set; }

    public decimal TaxTotal { get; private set; }

    public decimal Total { get; private set; }

    public string? Notes { get; private set; }

    public string? Terms { get; private set; }

    public string? CreditReason { get; private set; }

    public Guid? TemplateVersionId { get; private set; }

    public Guid? SourceDocumentId { get; private set; }

    public DateTimeOffset? IssuedAt { get; private set; }

    public int? PlanCount { get; private set; }

    public InstalmentFrequency? PlanFrequency { get; private set; }

    public DateTimeOffset? PlanRescheduledAt { get; private set; }

    /// <summary>Owner pause for reminders on this invoice (R30). Not part of the immutable issued content.</summary>
    public bool RemindersPaused { get; private set; }

    public QuoteAcceptance? Acceptance { get; private set; }

    /// <summary>Methods offered to the client on this document (DS §8.3 region 6).</summary>
    public AllowedPaymentMethods AllowedPaymentMethods { get; private set; } = AllowedPaymentMethods.All;

    /// <summary>SHA-256 hex of the client link token (R26); the token itself is never stored.</summary>
    public string? LinkTokenHash { get; private set; }

    /// <summary>The client link token, encrypted with Data Protection, so later sends and reminders reuse the same link (R26).</summary>
    public string? LinkTokenProtected { get; private set; }

    public IReadOnlyList<DocumentLine> Lines => _lines;

    public IReadOnlyList<Instalment> Instalments => _instalments;

    public Money TotalMoney => new(Total, Currency);

    public bool IsEditable => DocumentStateMachine.IsEditable(Status);

    public static Document CreateDraft(
        Guid tenantId,
        Guid businessId,
        DocumentType type,
        Guid clientId,
        CurrencyCode currency,
        DateTimeOffset now,
        Guid? sourceDocumentId = null)
    {
        var document = new Document
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Type = type,
            ClientId = clientId,
            Currency = currency,
            SourceDocumentId = sourceDocumentId,
        };
        document.Initialise(now);
        return document;
    }

    public Result<DocumentLine> AddLine(LineInput input, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return Result.Failure<DocumentLine>(editable.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            return Result.Failure<DocumentLine>(DocumentErrors.InvalidLine("A line needs a description."));
        }

        var line = DocumentLine.Create(this, _lines.Count + 1, input, now);
        _lines.Add(line);

        var recalculated = Recalculate();
        if (recalculated.IsFailure)
        {
            _lines.Remove(line);
            return Result.Failure<DocumentLine>(recalculated.Error);
        }

        Touch(now);
        return Result.Success(line);
    }

    public Result RemoveLine(int position, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        var line = _lines.Find(l => l.Position == position);
        if (line is null)
        {
            return Result.Failure(DocumentErrors.LineNotFound);
        }

        _lines.Remove(line);
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetPosition(i + 1);
        }

        var recalculated = Recalculate();
        if (recalculated.IsFailure)
        {
            return recalculated;
        }

        Touch(now);
        return Result.Success();
    }

    public Result SetDiscount(Discount discount, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        var previous = Discount;
        Discount = discount;

        var recalculated = Recalculate();
        if (recalculated.IsFailure)
        {
            Discount = previous;
            return recalculated;
        }

        Touch(now);
        return Result.Success();
    }

    public Result SetNotes(string? notes, string? terms, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        Notes = notes;
        Terms = terms;
        Touch(now);
        return Result.Success();
    }

    public Result SetCreditReason(string? reason, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (Type != DocumentType.CreditNote)
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.CreditNote));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(CreditNoteRules.ReasonRequired);
        }

        CreditReason = reason.Trim();
        Touch(now);
        return Result.Success();
    }

    public Result SetPlan(IReadOnlyList<InstalmentDraft> drafts, InstalmentFrequency? frequency, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (Type is not (DocumentType.Quote or DocumentType.Invoice))
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.Invoice));
        }

        var notAllowed = Type == DocumentType.Invoice ? InstalmentDueKind.OnAcceptance : InstalmentDueKind.OnIssue;
        if (drafts.Any(d => d.DueKind == notAllowed))
        {
            return Result.Failure(DocumentErrors.DueKindNotAllowed(Type));
        }

        var valid = InstalmentPlanner.Validate(drafts, Total, null);
        if (valid.IsFailure)
        {
            return valid;
        }

        _instalments.Clear();
        for (var i = 0; i < drafts.Count; i++)
        {
            _instalments.Add(Instalment.Create(this, i + 1, drafts[i], now));
        }

        PlanCount = drafts.Count;
        PlanFrequency = frequency;
        Touch(now);
        return Result.Success();
    }

    public Result ClearPlan(DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        _instalments.Clear();
        PlanCount = null;
        PlanFrequency = null;
        Touch(now);
        return Result.Success();
    }

    /// <summary>Draft header: client, currency and the proposed dates. Issue still sets the final dates (R9).</summary>
    public Result UpdateDraftHeader(Guid clientId, CurrencyCode currency, DateOnly? issueDate, DateOnly? dueDate, DateOnly? validUntil, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (issueDate is { } issue && ((dueDate is { } due && due < issue) || (validUntil is { } valid && valid < issue)))
        {
            return Result.Failure(DocumentErrors.DateBeforeIssue);
        }

        ClientId = clientId;
        Currency = currency;
        IssueDate = issueDate;
        DueDate = Type == DocumentType.Quote ? null : dueDate;
        ValidUntil = Type == DocumentType.Quote ? validUntil : null;
        Touch(now);
        return Result.Success();
    }

    /// <summary>Replaces every line with <paramref name="inputs"/> in order (positions 1..n). All-or-nothing.</summary>
    public Result ReplaceLines(IReadOnlyList<LineInput> inputs, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (inputs.Any(i => string.IsNullOrWhiteSpace(i.Description)))
        {
            return Result.Failure(DocumentErrors.InvalidLine("A line needs a description."));
        }

        var previous = _lines.ToList();
        _lines.Clear();
        for (var i = 0; i < inputs.Count; i++)
        {
            _lines.Add(DocumentLine.Create(this, i + 1, inputs[i], now));
        }

        var recalculated = Recalculate();
        if (recalculated.IsFailure)
        {
            _lines.Clear();
            _lines.AddRange(previous);
            Recalculate();
            return recalculated;
        }

        Touch(now);
        return Result.Success();
    }

    public Result SetAllowedPaymentMethods(AllowedPaymentMethods methods, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        AllowedPaymentMethods = methods & AllowedPaymentMethods.All;
        Touch(now);
        return Result.Success();
    }

    /// <summary>A new Draft of the same type and client with copies of the lines, discount, notes and methods. No number, dates or plan.</summary>
    public Document Duplicate(DateTimeOffset now, DocumentType? targetType = null, Guid? sourceDocumentId = null)
    {
        var copy = CreateDraft(TenantId, BusinessId, targetType ?? (Type == DocumentType.CreditNote ? DocumentType.Invoice : Type), ClientId, Currency, now, sourceDocumentId);
        copy.Discount = Discount;
        copy.Notes = Notes;
        copy.Terms = Terms;
        copy.AllowedPaymentMethods = AllowedPaymentMethods;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = DocumentLine.Create(copy, i + 1, _lines[i].ToInput(), now);
            line.CopyTaxSnapshot(_lines[i]);
            copy._lines.Add(line);
        }

        copy.Recalculate();
        return copy;
    }

    /// <summary>A Draft credit note against this issued invoice. Fraction 1 credits everything; lower values scale each line quantity (R13b).</summary>
    public Document DraftCreditNote(decimal fraction, DateTimeOffset now)
    {
        var copy = CreateDraft(TenantId, BusinessId, DocumentType.CreditNote, ClientId, Currency, now, Id);
        copy.Discount = Discount;
        for (var i = 0; i < _lines.Count; i++)
        {
            var input = _lines[i].ToInput() with { Quantity = Math.Round(_lines[i].Quantity * fraction, 4, MidpointRounding.AwayFromZero) };
            var line = DocumentLine.Create(copy, i + 1, input, now);
            line.CopyTaxSnapshot(_lines[i]);
            copy._lines.Add(line);
        }

        copy.Recalculate();
        return copy;
    }

    /// <summary>Converts an accepted quote once (R8) into a linked Draft invoice, copying lines and plan. "On acceptance" resolves to the acceptance date.</summary>
    public Result<Document> ConvertToInvoice(DateTimeOffset now)
    {
        var marked = MarkConverted();
        if (marked.IsFailure)
        {
            return Result.Failure<Document>(marked.Error);
        }

        var invoice = Duplicate(now, DocumentType.Invoice, Id);

        if (_instalments.Count > 0)
        {
            var drafts = _instalments
                .OrderBy(i => i.Position)
                .Select(i => i.ToDraft())
                .Select(d => d.DueKind == InstalmentDueKind.OnAcceptance ? d with { DueKind = InstalmentDueKind.Date } : d)
                .ToList();
            var plan = invoice.SetPlan(drafts, PlanFrequency, now);
            if (plan.IsFailure)
            {
                return Result.Failure<Document>(plan.Error);
            }
        }

        Touch(now);
        return Result.Success(invoice);
    }

    /// <summary>Locks lines, totals, currency, rate, snapshots, plan and template version (R5) and assigns the number (R6).</summary>
    public Result Issue(IssueRequest request, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (_lines.Count == 0)
        {
            return Result.Failure(DocumentErrors.NoLines);
        }

        if (request.TermsDays < 0)
        {
            return Result.Failure(DocumentErrors.TermsInvalid);
        }

        if (Type == DocumentType.CreditNote)
        {
            if (string.IsNullOrWhiteSpace(CreditReason))
            {
                return Result.Failure(CreditNoteRules.ReasonRequired);
            }

            if (SourceDocumentId is null)
            {
                return Result.Failure(CreditNoteRules.WrongInvoice);
            }
        }

        if (_instalments.Count > 0)
        {
            var resolved = _instalments
                .Select(i => i.DueKind == InstalmentDueKind.OnIssue ? i.ToDraft() with { DueDate = request.IssueDate } : i.ToDraft())
                .ToList();
            var valid = InstalmentPlanner.Validate(resolved, Total, request.IssueDate);
            if (valid.IsFailure)
            {
                return valid;
            }
        }

        var transition = DocumentStateMachine.EnsureTransition(Type, Status, DocumentStatus.Issued);
        if (transition.IsFailure)
        {
            return transition;
        }

        foreach (var instalment in _instalments.Where(i => i.DueKind == InstalmentDueKind.OnIssue))
        {
            instalment.ResolveDueDate(request.IssueDate, now);
        }

        Number = request.Number;
        Status = DocumentStatus.Issued;
        IssueDate = request.IssueDate;
        IssuedAt = now;
        ClientSnapshot = request.ClientSnapshot;
        BusinessSnapshot = request.BusinessSnapshot;
        TemplateVersionId = request.TemplateVersionId;
        ExchangeRate = request.ExchangeRate;
        LinkTokenHash = request.LinkTokenHash;
        LinkTokenProtected = request.LinkTokenProtected;

        if (Type == DocumentType.Invoice)
        {
            DueDate = _instalments.Count > 0
                ? _instalments.Max(i => i.DueDate)
                : DocumentCalculator.DueDate(request.IssueDate, request.TermsDays);
        }
        else if (Type == DocumentType.Quote)
        {
            ValidUntil = request.IssueDate.AddDays(request.QuoteValidityDays);
        }

        Touch(now);
        Raise(new DocumentIssued(Id, BusinessId, Type, request.Number.Value, now));
        return Result.Success();
    }

    /// <summary>Replaces the client link, so the old link stops working. Not part of the immutable content (R5).</summary>
    public Result RotateLinkToken(string tokenHash, DateTimeOffset now, string? protectedToken = null)
    {
        if (Status == DocumentStatus.Draft || Number is null)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Type, Status, DocumentStatus.Sent));
        }

        LinkTokenHash = tokenHash;
        LinkTokenProtected = protectedToken;
        Touch(now);
        return Result.Success();
    }

    /// <summary>Issued becomes Sent; later states are left alone because sending can repeat.</summary>
    public Result MarkSent(DateTimeOffset now)
    {
        if (Status == DocumentStatus.Draft)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Type, Status, DocumentStatus.Sent));
        }

        if (Status != DocumentStatus.Issued)
        {
            return Result.Success();
        }

        var transition = DocumentStateMachine.EnsureTransition(Type, Status, DocumentStatus.Sent);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = DocumentStatus.Sent;
        Touch(now);
        return Result.Success();
    }

    /// <summary>The first open sets Viewed; later opens do not change status (R27).</summary>
    public Result MarkViewed(DateTimeOffset now)
    {
        if (Status == DocumentStatus.Draft)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Type, Status, DocumentStatus.Viewed));
        }

        if (Status is not (DocumentStatus.Issued or DocumentStatus.Sent))
        {
            return Result.Success();
        }

        var transition = DocumentStateMachine.EnsureTransition(Type, Status, DocumentStatus.Viewed);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = DocumentStatus.Viewed;
        Touch(now);
        Raise(new DocumentViewed(Id, BusinessId, now));
        return Result.Success();
    }

    /// <summary>Records the signer's name, UTC time, IP and user agent (R8). Refused once expired (R7).</summary>
    public Result Accept(string signerName, string? ip, string? userAgent, string? comment, DateTimeOffset now) =>
        Decide(QuoteDecision.Accepted, signerName, ip, userAgent, comment, now);

    public Result Decline(string signerName, string? ip, string? userAgent, string? comment, DateTimeOffset now) =>
        Decide(QuoteDecision.Declined, signerName, ip, userAgent, comment, now);

    public Result Expire(DateTimeOffset now)
    {
        if (Type != DocumentType.Quote)
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.Quote));
        }

        var transition = DocumentStateMachine.EnsureTransition(Type, Status, DocumentStatus.Expired);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = DocumentStatus.Expired;
        Touch(now);
        return Result.Success();
    }

    /// <summary>A quote converts only once (R8).</summary>
    public Result MarkConverted()
    {
        if (Type != DocumentType.Quote)
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.Quote));
        }

        if (Status == DocumentStatus.Converted)
        {
            return Result.Failure(DocumentErrors.QuoteAlreadyConverted);
        }

        if (Status != DocumentStatus.Accepted)
        {
            return Result.Failure(DocumentErrors.QuoteNotAccepted);
        }

        Status = DocumentStatus.Converted;
        return Result.Success();
    }

    /// <summary>Moves an invoice to PartPaid, Paid or Void from credits and confirmed allocations (R17).</summary>
    public Result ApplyPaymentStatus(decimal creditedTotal, decimal confirmedPaid, DateTimeOffset now)
    {
        if (Type != DocumentType.Invoice)
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.Invoice));
        }

        var target = PaymentAllocator.InvoiceStatusFor(Status, Total, creditedTotal, confirmedPaid);
        if (target == Status)
        {
            return Result.Success();
        }

        var transition = DocumentStateMachine.EnsureTransition(Type, Status, target);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = target;
        Touch(now);
        return Result.Success();
    }

    public void SetRemindersPaused(bool paused, DateTimeOffset now)
    {
        RemindersPaused = paused;
        Touch(now);
    }

    public IReadOnlyList<InstalmentState> GetInstalmentStates(decimal confirmedPaid, decimal creditedTotal, DateOnly today) =>
        InstalmentStatusCalculator.Calculate(
            [.. _instalments.OrderBy(i => i.Position).Select(i => i.ToDraft())],
            confirmedPaid,
            creditedTotal,
            today);

    /// <summary>Owner-only reschedule of instalments with nothing paid (R36). Lines and totals never change.</summary>
    public Result RescheduleInstalments(
        IReadOnlyList<InstalmentDraft> proposed,
        string? reason,
        decimal confirmedPaid,
        decimal creditedTotal,
        DateOnly today,
        DateTimeOffset now)
    {
        if (Status == DocumentStatus.Draft || _instalments.Count == 0)
        {
            return Result.Failure(DocumentErrors.NotEditable);
        }

        var result = InstalmentPlanner.Reschedule(GetInstalmentStates(confirmedPaid, creditedTotal, today), proposed, reason);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        var ordered = _instalments.OrderBy(i => i.Position).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Replace(proposed[i], now);
        }

        DueDate = ordered.Max(i => i.DueDate);
        PlanRescheduledAt = now;
        Touch(now);
        return Result.Success();
    }

    private Result Decide(QuoteDecision decision, string signerName, string? ip, string? userAgent, string? comment, DateTimeOffset now)
    {
        if (Type != DocumentType.Quote)
        {
            return Result.Failure(DocumentErrors.WrongType(DocumentType.Quote));
        }

        if (string.IsNullOrWhiteSpace(signerName))
        {
            return Result.Failure(DocumentErrors.SignerRequired);
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (Status == DocumentStatus.Expired || (ValidUntil is { } validUntil && today > validUntil))
        {
            return Result.Failure(DocumentErrors.QuoteExpired);
        }

        var target = decision == QuoteDecision.Accepted ? DocumentStatus.Accepted : DocumentStatus.Declined;
        var transition = DocumentStateMachine.EnsureTransition(Type, Status, target);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = target;
        Acceptance = QuoteAcceptance.Create(this, decision, signerName, ip, userAgent, comment, now);

        if (decision == QuoteDecision.Accepted)
        {
            foreach (var instalment in _instalments.Where(i => i.DueKind == InstalmentDueKind.OnAcceptance))
            {
                instalment.ResolveDueDate(today, now);
            }

            Raise(new QuoteAccepted(Id, BusinessId, Acceptance.SignerName, now));
        }

        Touch(now);
        return Result.Success();
    }

    private Result EnsureEditable() =>
        IsEditable ? Result.Success() : Result.Failure(DocumentErrors.NotEditable);

    private Result Recalculate()
    {
        var inputs = _lines
            .Select(l => new LineCalcInput(l.Quantity, l.UnitPrice, l.LineDiscount, l.TaxPercentSnapshot))
            .ToList();

        var calculated = DocumentCalculator.Calculate(inputs, Discount);
        if (calculated.IsFailure)
        {
            return Result.Failure(calculated.Error);
        }

        var totals = calculated.Value;
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetLineTotal(totals.Lines[i].Amount - totals.Lines[i].LineDiscount);
        }

        Subtotal = totals.Subtotal;
        DiscountTotal = totals.DiscountTotal;
        TaxTotal = totals.TaxTotal;
        Total = totals.Total;
        return Result.Success();
    }
}
