using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Messaging;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Messaging;

public sealed class MessagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private static OutboundMessage NewMessage(MessageChannel channel = MessageChannel.WhatsApp) =>
        OutboundMessage.Queue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), channel, "+263771234567", "document_link", Now);

    [Fact]
    public void R28_delivery_status_only_moves_forward()
    {
        var message = NewMessage();
        message.MarkSent("wamid.1", Now);

        message.ApplyDelivery(MessageStatus.Read, null, Now);
        message.ApplyDelivery(MessageStatus.Delivered, null, Now);

        message.Status.ShouldBe(MessageStatus.Read);
    }

    [Fact]
    public void R28_failed_delivery_records_the_error()
    {
        var message = NewMessage();
        message.MarkSent("wamid.1", Now);

        message.ApplyDelivery(MessageStatus.Failed, "Number not on WhatsApp", Now);

        message.Status.ShouldBe(MessageStatus.Failed);
        message.Error.ShouldBe("Number not on WhatsApp");
    }

    [Fact]
    public void R28_failure_is_stored_for_the_document_to_show()
    {
        var message = NewMessage(MessageChannel.Email);

        message.MarkFailed("Email provider rejected the message (401).", Now);

        message.Status.ShouldBe(MessageStatus.Failed);
        message.Error.ShouldNotBeNull();
    }

    [Fact]
    public void R28_send_needs_at_least_one_channel()
    {
        var result = new SendDocumentValidator().Validate(new SendDocumentCommand(Guid.NewGuid(), null, null, null, null));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void R28_send_rejects_a_bad_email_address()
    {
        var result = new SendDocumentValidator().Validate(new SendDocumentCommand(Guid.NewGuid(), "not-an-email", null, null, null));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void R7_email_html_encodes_bound_values()
    {
        var content = EmailContent.Document("<b>Tendai</b>", "INV-0001", "$10.00", "https://x.test/d/abc?a=1&b=2", "<script>alert(1)</script>");

        content.Html.ShouldNotContain("<script>");
        content.Html.ShouldNotContain("<b>Tendai</b>");
        content.Html.ShouldContain("&amp;b=2");
    }

    [Fact]
    public void R28_email_text_part_carries_the_link()
    {
        var content = EmailContent.Document("Tendai", "INV-0001", "$10.00", "https://x.test/d/abc", null);

        content.Text.ShouldContain("https://x.test/d/abc");
        content.Subject.ShouldContain("INV-0001");
    }
}
