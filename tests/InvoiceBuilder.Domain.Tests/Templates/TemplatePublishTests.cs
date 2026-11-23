using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Templates;
using Shouldly;

namespace InvoiceBuilder.Domain.Tests.Templates;

public sealed class TemplatePublishTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Template NewTemplate() =>
        Template.Create(Guid.NewGuid(), Guid.NewGuid(), "Bold", [DocumentType.Invoice], Guid.NewGuid(), Now).Value;

    [Fact]
    public void R22_publishing_draft_creates_next_version_and_keeps_old()
    {
        var t = NewTemplate();
        var v1 = t.PublishVersion("{}", Guid.NewGuid(), Now);
        t.SaveDraft("{}", Now);
        var result = t.PublishDraft(Guid.NewGuid(), Now);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(2);
        t.CurrentVersionId.ShouldBe(result.Value.Id);
        t.Versions.ShouldContain(v1);
        t.DraftVersion.ShouldBeNull();
    }

    [Fact]
    public void R21_publish_without_draft_fails()
    {
        NewTemplate().PublishDraft(Guid.NewGuid(), Now).IsFailure.ShouldBeTrue();
    }
}
