using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Tests;

public sealed class DocumentStateMachineTests
{
    [Fact]
    public void R4_draft_to_issued_is_allowed()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m), TestData.Now);

        var result = invoice.Issue(TestData.NewIssueRequest("INV-0001", new DateOnly(2026, 10, 23)), TestData.Now);

        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Issued);
        invoice.Number!.Value.ShouldBe("INV-0001");
        invoice.DueDate.ShouldBe(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public void R5_editing_lines_after_issue_fails_with_not_editable()
    {
        var invoice = TestData.IssuedInvoice(100m, new DateOnly(2026, 10, 23));

        var result = invoice.AddLine(TestData.Line(1m, 5m), TestData.Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Document.NotEditable");
        invoice.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public void R5_removing_lines_after_issue_fails_with_not_editable()
    {
        var invoice = TestData.IssuedInvoice(100m, new DateOnly(2026, 10, 23));

        invoice.RemoveLine(1, TestData.Now).Error.Code.ShouldBe("Document.NotEditable");
    }

    [Fact]
    public void R5_issuing_a_document_without_lines_fails()
    {
        var invoice = TestData.DraftInvoice();

        invoice.Issue(TestData.NewIssueRequest("INV-0001", new DateOnly(2026, 10, 23)), TestData.Now).IsFailure.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void R5_issuing_twice_fails()
    {
        var invoice = TestData.IssuedInvoice(100m, new DateOnly(2026, 10, 23));

        invoice.Issue(TestData.NewIssueRequest("INV-0002", new DateOnly(2026, 10, 24)), TestData.Now).Error.Code.ShouldBe("Document.NotEditable");
        invoice.Number!.Value.ShouldBe("INV-0001");
    }

    [Fact]
    public void R8_accepted_quote_converts_only_once()
    {
        var quote = TestData.IssuedQuote();
        quote.Accept("Jane Doe", "10.0.0.1", "agent", null, TestData.Now).IsSuccess.ShouldBeTrue();

        quote.MarkConverted().IsSuccess.ShouldBeTrue();
        var second = quote.MarkConverted();

        second.IsFailure.ShouldBeTrue();
        second.Error.Code.ShouldBe("Quote.AlreadyConverted");
        quote.Status.ShouldBe(DocumentStatus.Converted);
    }

    [Fact]
    public void R8_quote_that_is_not_accepted_cannot_be_converted()
    {
        var quote = TestData.IssuedQuote();

        quote.MarkConverted().Error.Code.ShouldBe("Quote.NotAccepted");
    }

    [Fact]
    public void R8_accepting_records_signer_ip_and_user_agent()
    {
        var quote = TestData.IssuedQuote();

        quote.Accept("Jane Doe", "10.0.0.1", "agent", "ok", TestData.Now).IsSuccess.ShouldBeTrue();

        quote.Acceptance!.SignerName.ShouldBe("Jane Doe");
        quote.Acceptance.Ip.ShouldBe("10.0.0.1");
        quote.Acceptance.UserAgent.ShouldBe("agent");
        quote.Acceptance.AcceptedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public void R7_expired_quote_cannot_be_accepted()
    {
        var quote = TestData.IssuedQuote();
        quote.Expire(TestData.Now).IsSuccess.ShouldBeTrue();

        var result = quote.Accept("Jane Doe", null, null, null, TestData.Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Quote.Expired");
    }

    [Fact]
    public void R7_quote_past_its_validity_date_cannot_be_accepted()
    {
        var quote = TestData.IssuedQuote();

        var result = quote.Accept("Jane Doe", null, null, null, TestData.Now.AddDays(15));

        result.Error.Code.ShouldBe("Quote.Expired");
    }

    [Fact]
    public void R27_first_view_sets_viewed_and_later_views_do_not_change_status()
    {
        var invoice = TestData.IssuedInvoice(100m, new DateOnly(2026, 10, 23));

        invoice.MarkViewed(TestData.Now).IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Viewed);
        invoice.MarkViewed(TestData.Now).IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(DocumentStatus.Viewed);
    }

    [Fact]
    public void R4_issuing_raises_a_domain_event()
    {
        var invoice = TestData.IssuedInvoice(100m, new DateOnly(2026, 10, 23));

        invoice.DomainEvents.OfType<DocumentIssued>().ShouldHaveSingleItem();
    }
}
