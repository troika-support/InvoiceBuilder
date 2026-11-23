using InvoiceBuilder.Application.Messaging;
using Shouldly;
using Xunit;

namespace InvoiceBuilder.Application.Tests.Messaging;

public sealed class ReminderEmailContentTests
{
    [Fact]
    public void R30_reminder_note_uses_reminder_subject()
    {
        var content = EmailContent.ReminderNote("Tariro", "INV-0001", "USD 100.00 is due 5 Mar 2026", "https://x/d/t", false);

        content.Subject.ShouldBe("Reminder: INV-0001");
        content.Text.ShouldContain("USD 100.00 is due 5 Mar 2026");
        content.Text.ShouldContain("https://x/d/t");
    }

    [Fact]
    public void R39_instalment_reminder_note_uses_instalment_subject()
    {
        var content = EmailContent.ReminderNote("Tariro", "INV-0001", "Instalment 2 of 3", "https://x/d/t", true);

        content.Subject.ShouldStartWith("Instalment reminder");
    }

    [Fact]
    public void R28_receipt_content_mentions_amount_received()
    {
        var content = EmailContent.Receipt("Tariro", "RCT-0001", "USD 50.00", "https://x/d/t");

        content.Subject.ShouldBe("Receipt for RCT-0001");
        content.Text.ShouldContain("USD 50.00");
    }

    [Fact]
    public void R28_reminder_content_is_html_encoded()
    {
        var content = EmailContent.ReminderNote("<b>Eve</b>", "INV-1", "note", "https://x", false);

        content.Html.ShouldNotContain("<b>Eve</b>");
    }
}
