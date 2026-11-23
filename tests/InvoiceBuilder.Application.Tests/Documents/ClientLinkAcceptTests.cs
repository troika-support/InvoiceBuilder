using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Documents;

public sealed class ClientLinkAcceptTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IScopeOverride _scope = Substitute.For<IScopeOverride>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly InvoiceBuilder.Application.Notifications.INotificationRepository _notifications = Substitute.For<InvoiceBuilder.Application.Notifications.INotificationRepository>();

    private Document IssuedQuote(string token)
    {
        var quote = Document.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Quote, Guid.NewGuid(), CurrencyCode.Usd, Now);
        quote.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), Now).IsSuccess.ShouldBeTrue();
        quote.Issue(new IssueRequest(DocumentNumber.Create("QUO-0001").Value, new DateOnly(2026, 10, 23), 14, "{}", "{}", Guid.NewGuid(), null, 14, ClientLinkToken.Hash(token)), Now).IsSuccess.ShouldBeTrue();
        _documents.GetByLinkTokenHashAsync(ClientLinkToken.Hash(token), Arg.Any<CancellationToken>()).Returns(quote);
        return quote;
    }

    [Fact]
    public async Task R26_unknown_token_is_rejected_as_invalid()
    {
        var handler = new AcceptQuoteHandler(_documents, _scope, _activity, _notifications, new FakeTimeProvider(Now));
        var result = await handler.Handle(new AcceptQuoteCommand("nope", "Jane Doe", null, null), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ClientLinkErrors.Invalid.Code);
    }

    [Fact]
    public async Task R26_valid_token_accepts_quote_in_the_documents_scope()
    {
        var quote = IssuedQuote("tok");
        var handler = new AcceptQuoteHandler(_documents, _scope, _activity, _notifications, new FakeTimeProvider(Now));
        var result = await handler.Handle(new AcceptQuoteCommand("tok", "Jane Doe", null, null), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        quote.Status.ShouldBe(DocumentStatus.Accepted);
        _scope.Received().UseScope(quote.TenantId, quote.BusinessId);
        _notifications.Received(1).Add(Arg.Is<InvoiceBuilder.Domain.Notifications.Notification>(n =>
            n.Kind == InvoiceBuilder.Domain.Notifications.NotificationKind.QuoteAccepted
            && n.BusinessId == quote.BusinessId
            && n.LinkPath == $"/quotes/{quote.Id}"));
    }

    [Fact]
    public async Task Decline_adds_a_quote_declined_notification()
    {
        var quote = IssuedQuote("tok");
        var handler = new DeclineQuoteHandler(_documents, _scope, _activity, _notifications, new FakeTimeProvider(Now));
        var result = await handler.Handle(new DeclineQuoteCommand("tok", "Jane Doe", "Too pricey", null, null), CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        _notifications.Received(1).Add(Arg.Is<InvoiceBuilder.Domain.Notifications.Notification>(n =>
            n.Kind == InvoiceBuilder.Domain.Notifications.NotificationKind.QuoteDeclined && n.BusinessId == quote.BusinessId));
    }

    [Fact]
    public async Task Unknown_token_adds_no_notification()
    {
        var handler = new AcceptQuoteHandler(_documents, _scope, _activity, _notifications, new FakeTimeProvider(Now));
        await handler.Handle(new AcceptQuoteCommand("nope", "Jane Doe", null, null), CancellationToken.None);
        _notifications.DidNotReceive().Add(Arg.Any<InvoiceBuilder.Domain.Notifications.Notification>());
    }

    [Fact]
    public async Task R26_a_quote_cannot_be_accepted_twice()
    {
        IssuedQuote("tok");
        var handler = new AcceptQuoteHandler(_documents, _scope, _activity, _notifications, new FakeTimeProvider(Now));
        (await handler.Handle(new AcceptQuoteCommand("tok", "Jane Doe", null, null), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await handler.Handle(new AcceptQuoteCommand("tok", "Jane Doe", null, null), CancellationToken.None)).IsFailure.ShouldBeTrue();
    }
}
