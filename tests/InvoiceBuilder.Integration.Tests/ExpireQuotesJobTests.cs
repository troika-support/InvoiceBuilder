using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;
using InvoiceBuilder.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace InvoiceBuilder.Integration.Tests;

public sealed class ExpireQuotesJobTests
{
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly FakeTimeProvider _clock = new();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();

    private ExpireQuotesJob Job() => new(_documents, Substitute.For<IScopeOverride>(), _unitOfWork, _activity, _clock, NullLogger<ExpireQuotesJob>.Instance);

    private Document IssuedQuote()
    {
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var quote = Document.CreateDraft(_tenant, _business, DocumentType.Quote, Guid.NewGuid(), CurrencyCode.Usd, created);
        quote.AddLine(new LineInput("Item", null, 1m, 100m, 0m, null), created).IsSuccess.ShouldBeTrue();
        var request = new IssueRequest(DocumentNumber.Create("QUO-0001").Value, new DateOnly(2026, 1, 1), 14, "{}", "{}", Guid.NewGuid());
        quote.Issue(request, created).IsSuccess.ShouldBeTrue();
        _documents.ListBusinessesWithExpiredQuotesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid TenantId, Guid BusinessId)>>([(_tenant, _business)]));
        _documents.ListExpiredQuotesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Document>>([quote]));
        return quote;
    }

    [Fact]
    public async Task R7_overdue_quote_is_expired_and_audited()
    {
        var quote = IssuedQuote();
        _clock.SetUtcNow(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        await Job().RunAsync(CancellationToken.None);
        quote.Status.ShouldBe(DocumentStatus.Expired);
        await _activity.Received(1).AddAsync("Document", quote.Id, "QuoteExpired", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R7_rerunning_does_not_audit_an_already_expired_quote_again()
    {
        var quote = IssuedQuote();
        _clock.SetUtcNow(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        await Job().RunAsync(CancellationToken.None);
        await Job().RunAsync(CancellationToken.None);
        quote.Status.ShouldBe(DocumentStatus.Expired);
        await _activity.Received(1).AddAsync("Document", quote.Id, "QuoteExpired", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task R7_no_businesses_with_overdue_quotes_does_nothing()
    {
        _documents.ListBusinessesWithExpiredQuotesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid TenantId, Guid BusinessId)>>([]));
        await Job().RunAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
