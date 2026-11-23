using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Tests;

public sealed class QuoteExpiryTests
{
    [Fact]
    public void R7_issued_quote_can_expire()
    {
        var quote = TestData.IssuedQuote();
        quote.Expire(TestData.Now).IsSuccess.ShouldBeTrue();
        quote.Status.ShouldBe(DocumentStatus.Expired);
    }

    [Fact]
    public void R7_expiring_twice_fails_so_the_job_rerun_changes_nothing()
    {
        var quote = TestData.IssuedQuote();
        quote.Expire(TestData.Now).IsSuccess.ShouldBeTrue();
        quote.Expire(TestData.Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void R7_accepted_quote_does_not_expire()
    {
        var quote = TestData.IssuedQuote();
        quote.Accept("Jane Doe", "10.0.0.1", "agent", null, TestData.Now).IsSuccess.ShouldBeTrue();
        quote.Expire(TestData.Now).IsFailure.ShouldBeTrue();
        quote.Status.ShouldBe(DocumentStatus.Accepted);
    }
}
