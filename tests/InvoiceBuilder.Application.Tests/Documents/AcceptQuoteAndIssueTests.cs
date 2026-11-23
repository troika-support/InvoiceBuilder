using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Behaviours;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using MediatR;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class AcceptQuoteAndIssueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IScopeOverride _scope = Substitute.For<IScopeOverride>();

    private Document DepositQuote(string token)
    {
        var quote = Document.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Quote, Guid.NewGuid(), CurrencyCode.Usd, Now);
        quote.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        quote.SetPlan([new InstalmentDraft(InstalmentDueKind.OnAcceptance, null, 40m), new InstalmentDraft(InstalmentDueKind.Date, new DateOnly(2026, 12, 1), 60m)], InstalmentFrequency.Monthly, Now).IsSuccess.ShouldBeTrue();
        quote.Issue(new IssueRequest(DocumentNumber.Create("QUO-0001").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid(), null, 14, ClientLinkToken.Hash(token)), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), Arg.Any<CancellationToken>()).Returns(quote);
        return quote;
    }

    [Fact]
    public async Task R35_failed_conversion_fails_the_outcome_and_never_issues()
    {
        var quote = DepositQuote("tok");
        _sender.Send(Arg.Any<AcceptQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sender.Send(Arg.Any<ConvertQuoteToInvoiceCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Failure<Guid>(new Error("Test.Fail", "boom")));

        var result = await new AcceptQuoteAndIssueHandler(_sender, _documents, _scope).Handle(new AcceptQuoteAndIssueCommand("tok", "Jane Doe", null, null), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _sender.Received(1).Send(Arg.Is<ConvertQuoteToInvoiceCommand>(c => c.QuoteId == quote.Id), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<IssueDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R35_failed_outer_command_is_rolled_back_not_committed()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<ITransactionScope>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        var behaviour = new UnitOfWorkBehaviour<AcceptQuoteAndIssueCommand, Result<QuoteAcceptanceOutcome>>(unitOfWork, Substitute.For<IDomainEventDispatcher>());

        var response = await behaviour.Handle(
            new AcceptQuoteAndIssueCommand("tok", "Jane Doe", null, null),
            _ => Task.FromResult(Result.Failure<QuoteAcceptanceOutcome>(new Error("Test.Fail", "boom"))),
            CancellationToken.None);

        response.IsFailure.ShouldBeTrue();
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
