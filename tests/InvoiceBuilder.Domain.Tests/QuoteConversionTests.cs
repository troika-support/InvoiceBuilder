using InvoiceBuilder.Domain.Documents;
using Shouldly;

namespace InvoiceBuilder.Domain.Tests;

public sealed class QuoteConversionTests
{
    [Fact]
    public void R8_converting_accepted_quote_creates_linked_draft_invoice()
    {
        var quote = TestData.IssuedQuote();
        quote.Accept("Jane Doe", "10.0.0.1", "agent", null, TestData.Now).IsSuccess.ShouldBeTrue();
        var result = quote.ConvertToInvoice(TestData.Now);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(DocumentType.Invoice);
        result.Value.SourceDocumentId.ShouldBe(quote.Id);
        quote.Status.ShouldBe(DocumentStatus.Converted);
        quote.ConvertToInvoice(TestData.Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void R8_converting_unaccepted_quote_fails()
    {
        TestData.IssuedQuote().ConvertToInvoice(TestData.Now).Error.Code.ShouldBe("Quote.NotAccepted");
    }
}
