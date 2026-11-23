using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using MediatR;

namespace InvoiceBuilder.Application.Documents;

/// <summary>Queues the stored PDF render for an issued document (template-rendering.md §5, queue "pdf").</summary>
public interface IPdfJobScheduler
{
    void Enqueue(Guid documentId);
}

public static class ClientLinkToken
{
    /// <summary>32 random bytes, base64url (R26).</summary>
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class PdfKeys
{
    public static string For(Guid documentId, Guid templateVersionId) => $"pdf/{documentId:N}-{templateVersionId:N}.pdf";
}

public sealed record IssuedDocument(Guid DocumentId, string Number, string LinkToken);

public static class IssueDocumentErrors
{
    public static readonly Error TemplateNotFound = new("Document.TemplateNotFound", "Choose a published template.");

    public static readonly Error TemplateWrongType = new("Document.TemplateWrongType", "That template is not offered for this document type.");
}

/// <summary>Issues a draft (R5, R6) and then runs the post-commit work: fiscalisation (R32) and the PDF job.</summary>
public sealed record IssueDocumentCommand(Guid DocumentId, Guid TemplateVersionId, int? TermsDays = null, decimal? ExchangeRate = null)
    : IRequest<Result<IssuedDocument>>;

public sealed class IssueDocumentValidator : AbstractValidator<IssueDocumentCommand>
{
    public IssueDocumentValidator()
    {
        RuleFor(c => c.DocumentId).NotEmpty();
        RuleFor(c => c.TemplateVersionId).NotEmpty().WithMessage("Choose a published template.");
        RuleFor(c => c.TermsDays).GreaterThanOrEqualTo(0).When(c => c.TermsDays is not null);
        RuleFor(c => c.ExchangeRate).GreaterThan(0).When(c => c.ExchangeRate is not null);
    }
}

public sealed class IssueDocumentHandler(
    ISender sender,
    IDocumentRepository documents,
    IFiscalisationService fiscalisation,
    IPdfJobScheduler pdfJobs) : IRequestHandler<IssueDocumentCommand, Result<IssuedDocument>>
{
    public async Task<Result<IssuedDocument>> Handle(IssueDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issued = await sender.Send(new IssueDocumentInTransactionCommand(request.DocumentId, request.TemplateVersionId, request.TermsDays, request.ExchangeRate), cancellationToken);
        if (issued.IsFailure)
        {
            return issued;
        }

        var document = await documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is { Type: DocumentType.Invoice or DocumentType.CreditNote })
        {
            await fiscalisation.QueueAsync(document.Id, cancellationToken);
        }

        pdfJobs.Enqueue(request.DocumentId);
        return issued;
    }
}

public sealed record IssueDocumentInTransactionCommand(Guid DocumentId, Guid TemplateVersionId, int? TermsDays, decimal? ExchangeRate)
    : ITransactionalCommand<Result<IssuedDocument>>;

public sealed class IssueDocumentInTransactionHandler(
    ICurrentBusiness currentBusiness,
    IDocumentRepository documents,
    IBusinessRepository businesses,
    IClientRepository clients,
    ITemplateRepository templates,
    INumberSequenceRepository numbers,
    IActivityLog activity,
    IClientLinkTokenProtector linkProtector,
    TimeProvider timeProvider) : IRequestHandler<IssueDocumentInTransactionCommand, Result<IssuedDocument>>
{
    private static readonly TimeSpan HarareOffset = TimeSpan.FromHours(2);

    public async Task<Result<IssuedDocument>> Handle(IssueDocumentInTransactionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();

        var loaded = await DraftLoader.LoadAsync(currentBusiness, documents, request.DocumentId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<IssuedDocument>(loaded.Error);
        }

        var document = loaded.Value;
        var version = await templates.GetVersionAsync(request.TemplateVersionId, cancellationToken);
        if (version is null || version.BusinessId != document.BusinessId || version.PublishedAt is null)
        {
            return Result.Failure<IssuedDocument>(IssueDocumentErrors.TemplateNotFound);
        }

        var template = await templates.GetByIdAsync(version.TemplateId, cancellationToken);
        if (template is null || !template.DocumentTypes.Contains(document.Type))
        {
            return Result.Failure<IssuedDocument>(IssueDocumentErrors.TemplateWrongType);
        }

        var client = await clients.GetByIdAsync(document.ClientId, cancellationToken);
        var business = await businesses.GetByIdAsync(document.BusinessId, cancellationToken);
        if (client is null || business is null)
        {
            return Result.Failure<IssuedDocument>(DraftDocumentErrors.ClientNotFound);
        }

        var number = await numbers.AllocateAsync(document.BusinessId, document.Type, cancellationToken);
        if (number.IsFailure)
        {
            return Result.Failure<IssuedDocument>(number.Error);
        }

        var token = ClientLinkToken.Generate();
        var issueRequest = new IssueRequest(
            number.Value,
            DateOnly.FromDateTime(now.ToOffset(HarareOffset).DateTime),
            request.TermsDays ?? business.DefaultTermsDays,
            JsonSerializer.Serialize(new { name = client.Name, email = client.Email, phone = client.Phone, whatsApp = client.WhatsApp, address = client.Address }),
            JsonSerializer.Serialize(new { tradingName = business.TradingName, registeredName = business.RegisteredName, email = business.Email, whatsApp = business.WhatsAppNumber, address = business.Address }),
            version.Id,
            request.ExchangeRate,
            business.QuoteValidityDays,
            ClientLinkToken.Hash(token),
            linkProtector.Protect(token));

        var issued = document.Issue(issueRequest, now);
        if (issued.IsFailure)
        {
            return Result.Failure<IssuedDocument>(issued.Error);
        }

        await activity.AddAsync(nameof(Document), document.Id, "DocumentIssued", $"{document.Type} {number.Value} issued to {client.Name}.", cancellationToken);
        return Result.Success(new IssuedDocument(document.Id, number.Value.Value, token));
    }
}
