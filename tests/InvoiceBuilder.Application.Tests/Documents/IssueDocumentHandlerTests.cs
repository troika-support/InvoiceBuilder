using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class IssueDocumentHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IFiscalisationService _fiscalisation = Substitute.For<IFiscalisationService>();
    private readonly IPdfJobScheduler _pdfJobs = Substitute.For<IPdfJobScheduler>();

    private IssueDocumentHandler Handler() => new(_sender, _documents, _fiscalisation, _pdfJobs);

    private Document Draft(DocumentType type = DocumentType.Invoice)
    {
        var document = Document.CreateDraft(TenantId, BusinessId, type, Guid.NewGuid(), CurrencyCode.Usd, Now);
        _documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        return document;
    }

    [Fact]
    public async Task R5_failed_issue_queues_no_pdf_and_no_fiscalisation()
    {
        var document = Draft();
        _sender.Send(Arg.Any<IssueDocumentInTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IssuedDocument>(IssueDocumentErrors.TemplateNotFound));

        var result = await Handler().Handle(new IssueDocumentCommand(document.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        _pdfJobs.DidNotReceive().Enqueue(Arg.Any<Guid>());
        await _fiscalisation.DidNotReceive().QueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R32_issued_invoice_is_queued_for_fiscalisation_and_pdf()
    {
        var document = Draft();
        _sender.Send(Arg.Any<IssueDocumentInTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new IssuedDocument(document.Id, "INV-0001", "token")));

        var result = await Handler().Handle(new IssueDocumentCommand(document.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _fiscalisation.Received(1).QueueAsync(document.Id, Arg.Any<CancellationToken>());
        _pdfJobs.Received(1).Enqueue(document.Id);
    }

    [Fact]
    public async Task R32_issued_quote_is_not_fiscalised_but_still_gets_a_pdf()
    {
        var document = Draft(DocumentType.Quote);
        _sender.Send(Arg.Any<IssueDocumentInTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new IssuedDocument(document.Id, "QUO-0001", "token")));

        await Handler().Handle(new IssueDocumentCommand(document.Id, Guid.NewGuid()), CancellationToken.None);

        await _fiscalisation.DidNotReceive().QueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _pdfJobs.Received(1).Enqueue(document.Id);
    }
}
