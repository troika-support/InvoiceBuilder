using InvoiceBuilder.Application.Templates;

namespace InvoiceBuilder.Application.Tests.Templates;

public sealed class TemplateDefinitionValidatorTests
{
    private static TemplateDefinition With(params TemplateElement[] elements) =>
        new() { FirstPage = new PageLayer { Elements = elements } };

    [Fact]
    public void Valid_definition_passes() =>
        TemplateDefinitionValidator.Validate(With(new TemplateElement { Id = "a", Type = ElementTypes.Field, Binding = "client.name", XMm = 10, YMm = 10, WidthMm = 50, HeightMm = 8 }))
            .IsSuccess.ShouldBeTrue();

    [Fact]
    public void Unknown_binding_is_rejected() =>
        TemplateDefinitionValidator.Validate(With(new TemplateElement { Id = "a", Type = ElementTypes.Field, Binding = "client.secretNotes", WidthMm = 10, HeightMm = 5 }))
            .Error.Code.ShouldBe("Template.UnknownBinding");

    [Fact]
    public void Unknown_binding_inside_text_is_rejected() =>
        TemplateDefinitionValidator.Validate(With(new TemplateElement { Id = "a", Type = ElementTypes.Text, Text = "Hi {{document.__proto__}}", WidthMm = 10, HeightMm = 5 }))
            .Error.Code.ShouldBe("Template.UnknownBinding");

    [Fact]
    public void Element_outside_page_is_rejected() =>
        TemplateDefinitionValidator.Validate(With(new TemplateElement { Id = "a", Type = ElementTypes.Shape, XMm = 200, YMm = 10, WidthMm = 30, HeightMm = 5 }))
            .Error.Code.ShouldBe("Template.OutsidePage");

    [Fact]
    public void Unknown_element_type_is_rejected() =>
        TemplateDefinitionValidator.Validate(With(new TemplateElement { Id = "a", Type = "script", WidthMm = 10, HeightMm = 5 }))
            .Error.Code.ShouldBe("Template.UnknownElement");
}
