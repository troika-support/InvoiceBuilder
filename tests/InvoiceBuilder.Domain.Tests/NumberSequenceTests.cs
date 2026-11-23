using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Tests;

public sealed class NumberSequenceTests
{
    [Fact]
    public void R6_numbers_are_sequential_and_never_reused()
    {
        var sequence = NumberSequence.CreateDefault(TestData.TenantId, TestData.BusinessId, DocumentType.Invoice, TestData.Now);

        var first = sequence.Allocate(TestData.Now).Value;
        var second = sequence.Allocate(TestData.Now).Value;

        first.Value.ShouldBe("INV-0001");
        second.Value.ShouldBe("INV-0002");
        sequence.NextValue.ShouldBe(3);
    }

    [Theory]
    [InlineData(DocumentType.Quote, "QUO-0001")]
    [InlineData(DocumentType.Invoice, "INV-0001")]
    [InlineData(DocumentType.Receipt, "RCT-0001")]
    [InlineData(DocumentType.CreditNote, "CRN-0001")]
    public void R6_default_prefixes_per_document_type(DocumentType type, string expected) =>
        NumberSequence.CreateDefault(TestData.TenantId, TestData.BusinessId, type, TestData.Now)
            .Allocate(TestData.Now).Value.Value.ShouldBe(expected);

    [Fact]
    public void R1_every_entity_carries_tenant_and_business_ids()
    {
        var invoice = TestData.DraftInvoice();

        invoice.TenantId.ShouldBe(TestData.TenantId);
        invoice.BusinessId.ShouldBe(TestData.BusinessId);
        invoice.Id.ShouldNotBe(Guid.Empty);
        invoice.CreatedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public void R6_prefix_change_affects_future_numbers_only()
    {
        var sequence = NumberSequence.CreateDefault(TestData.TenantId, TestData.BusinessId, DocumentType.Invoice, TestData.Now);
        sequence.Allocate(TestData.Now);

        sequence.ChangePrefix("TRK-", TestData.Now).IsSuccess.ShouldBeTrue();

        sequence.NextValue.ShouldBe(2);
        sequence.Allocate(TestData.Now).Value.Value.ShouldBe("TRK-0002");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("WAY-TOO-LONG-PREFIX-XX")]
    [InlineData("IN V")]
    public void R6_invalid_prefix_is_rejected(string prefix)
    {
        var sequence = NumberSequence.CreateDefault(TestData.TenantId, TestData.BusinessId, DocumentType.Invoice, TestData.Now);

        sequence.ChangePrefix(prefix, TestData.Now).IsFailure.ShouldBeTrue();
        sequence.Prefix.ShouldBe("INV-");
    }
}
