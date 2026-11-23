using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Tests;

public sealed class IssueRulesTests
{
    private static readonly DateOnly IssueDay = new(2026, 10, 23);

    [Fact]
    public void R34_on_issue_instalment_resolves_to_issue_date_and_invoice_due_date_is_last_instalment()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m), TestData.Now).IsSuccess.ShouldBeTrue();
        invoice.SetPlan(
            [
                new InstalmentDraft(InstalmentDueKind.OnIssue, null, 40m),
                new InstalmentDraft(InstalmentDueKind.Date, IssueDay.AddDays(30), 60m),
            ],
            InstalmentFrequency.Monthly,
            TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.Issue(TestData.NewIssueRequest("INV-0001", IssueDay), TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.Instalments.OrderBy(i => i.Position).First().DueDate.ShouldBe(IssueDay);
        invoice.DueDate.ShouldBe(IssueDay.AddDays(30));
    }

    [Fact]
    public void R34_plan_that_does_not_add_up_to_the_total_is_rejected()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m), TestData.Now).IsSuccess.ShouldBeTrue();

        var result = invoice.SetPlan(
            [
                new InstalmentDraft(InstalmentDueKind.Date, IssueDay.AddDays(7), 40m),
                new InstalmentDraft(InstalmentDueKind.Date, IssueDay.AddDays(30), 50m),
            ],
            InstalmentFrequency.Monthly,
            TestData.Now);

        result.IsFailure.ShouldBeTrue();
        invoice.Instalments.ShouldBeEmpty();
    }

    [Fact]
    public void R5_snapshots_and_template_version_are_locked_at_issue()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m), TestData.Now).IsSuccess.ShouldBeTrue();
        var request = TestData.NewIssueRequest("INV-0001", IssueDay);

        invoice.Issue(request, TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.TemplateVersionId.ShouldBe(request.TemplateVersionId);
        invoice.UpdateDraftHeader(Guid.NewGuid(), invoice.Currency, IssueDay, null, null, TestData.Now).IsFailure.ShouldBeTrue();
        invoice.SetNotes("changed", null, TestData.Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void R26_issue_stores_only_the_link_token_hash()
    {
        var invoice = TestData.DraftInvoice();
        invoice.AddLine(TestData.Line(1m, 100m), TestData.Now).IsSuccess.ShouldBeTrue();
        var request = TestData.NewIssueRequest("INV-0001", IssueDay) with { LinkTokenHash = "abc123" };

        invoice.Issue(request, TestData.Now).IsSuccess.ShouldBeTrue();

        invoice.LinkTokenHash.ShouldBe("abc123");
    }
}
