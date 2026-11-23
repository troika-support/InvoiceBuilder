using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.ValueObjects;

namespace InvoiceBuilder.Domain.Tests;

public sealed class CreditNoteRulesTests
{
    private static readonly DateOnly InvoiceDate = new(2026, 1, 15);

    private static (Document Invoice, Businesses.TaxRate Vat) VatInvoice()
    {
        var vat = TestData.Vat(15m);
        return (TestData.IssuedInvoice(1000m, InvoiceDate, vat), vat);
    }

    [Fact]
    public void R13b_valid_credit_note_passes()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, vat);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void R13b_reason_is_required()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, vat, reason: null);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.ReasonRequired");
    }

    [Fact]
    public void R13b_currency_must_match_the_invoice()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, vat, currency: CurrencyCode.Zwg);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.CurrencyMismatch");
    }

    [Fact]
    public void R13b_tax_rate_not_on_the_invoice_fails()
    {
        var (invoice, _) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, TestData.Vat(15m));

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.TaxRateNotOnInvoice");
    }

    [Fact]
    public void R13b_credit_above_the_invoice_total_fails()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 1200m, vat);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.ExceedsInvoice");
    }

    [Fact]
    public void R13b_credits_across_notes_cannot_exceed_the_invoice_total()
    {
        var (invoice, vat) = VatInvoice();
        var earlier = TestData.DraftCreditNote(invoice, 1000m, vat);
        var note = TestData.DraftCreditNote(invoice, 100m, vat);

        CreditNoteRules.Validate(invoice, [earlier], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.ExceedsInvoice");
    }

    [Fact]
    public void R13b_credit_note_exactly_at_12_months_is_allowed()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, vat);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2027, 1, 15)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void R13b_credit_note_12_months_and_one_day_after_the_invoice_fails()
    {
        var (invoice, vat) = VatInvoice();
        var note = TestData.DraftCreditNote(invoice, 500m, vat);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2027, 1, 16)).Error.Code.ShouldBe("CreditNote.TooLate");
    }

    [Fact]
    public void R13b_credit_note_must_reference_the_invoice()
    {
        var (invoice, vat) = VatInvoice();
        var other = TestData.IssuedInvoice(1000m, InvoiceDate, vat);
        var note = TestData.DraftCreditNote(other, 500m, vat);

        CreditNoteRules.Validate(invoice, [], note, new DateOnly(2026, 2, 1)).Error.Code.ShouldBe("CreditNote.WrongInvoice");
    }
}
