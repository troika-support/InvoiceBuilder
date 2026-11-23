using InvoiceBuilder.Application.Templates;
using InvoiceBuilder.Infrastructure.Rendering;
using Shouldly;

namespace InvoiceBuilder.Integration.Tests.Rendering;

public sealed class BuiltInTemplateTests
{
    [Theory]
    [InlineData("Minimal")]
    [InlineData("Bold header")]
    [InlineData("Classic ledger")]
    public void Built_in_is_valid_and_has_instalment_schedule(string name)
    {
        var json = new BuiltInTemplates().Load(name);
        json.ShouldNotBeNull();
        var parsed = TemplateDefinitionJson.Parse(json);
        parsed.IsSuccess.ShouldBeTrue();
        TemplateDefinitionValidator.Validate(parsed.Value).IsSuccess.ShouldBeTrue();
        parsed.Value.Flow.InstalmentSchedule.ShouldNotBeNull();
    }
}
