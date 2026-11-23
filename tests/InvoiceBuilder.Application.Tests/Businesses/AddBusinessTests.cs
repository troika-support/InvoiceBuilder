using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Businesses.Settings;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Templates;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Businesses;

public sealed class AddBusinessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly INumberSequenceRepository _sequences = Substitute.For<INumberSequenceRepository>();
    private readonly ITemplateRepository _templates = Substitute.For<ITemplateRepository>();
    private readonly IUserPreferenceStore _preferences = Substitute.For<IUserPreferenceStore>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();

    private AddBusinessHandler Handler()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantId);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("owner-1");
        return new AddBusinessHandler(tenant, user, _businesses, _sequences, _templates, _preferences, _activity, TimeProvider.System);
    }

    [Fact]
    public async Task R6_new_business_gets_default_sequences_theme_and_is_selected()
    {
        _businesses.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var added = new List<NumberSequence>();
        _sequences.Add(Arg.Do<NumberSequence>(added.Add));
        Theme? theme = null;
        _templates.AddTheme(Arg.Do<Theme>(t => theme = t));
        Business? business = null;
        _businesses.Add(Arg.Do<Business>(b => business = b));

        var result = await Handler().Handle(new AddBusinessCommand("Troika", "Troika (Pvt) Ltd", "hi@troika.co.zw", "Harare"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.Select(s => s.Prefix).ShouldBe(["QUO-", "INV-", "RCT-", "CRN-"]);
        added.ShouldAllBe(s => s.BusinessId == result.Value && s.NextValue == 1);
        theme.ShouldNotBeNull();
        business!.DefaultThemeId.ShouldBe(theme.Id);
        business.TenantId.ShouldBe(TenantId);
        await _preferences.Received(1).SetSelectedBusinessAsync("owner-1", result.Value, Arg.Any<CancellationToken>());
        await _activity.Received(1).AddAsync(nameof(Business), result.Value, "BusinessAdded", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_trading_name_is_rejected()
    {
        var existing = Business.Create(TenantId, "Troika", "", "a@b.co", "", DateTimeOffset.UtcNow);
        _businesses.ListAsync(Arg.Any<CancellationToken>()).Returns([existing]);

        var result = await Handler().Handle(new AddBusinessCommand(" troika ", "", "a@b.co", ""), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AddBusinessHandler.DuplicateName);
        _sequences.DidNotReceive().Add(Arg.Any<NumberSequence>());
    }

    [Fact]
    public void Invalid_command_reports_field_errors()
    {
        var errors = new AddBusinessValidator().Validate(new AddBusinessCommand("", "", "not-an-email", "")).Errors;

        errors.Select(e => e.PropertyName).ShouldBe(["TradingName", "Email"], ignoreOrder: true);
    }

    [Fact]
    public void R6_prefix_with_spaces_is_invalid()
    {
        var command = new UpdateBusinessProfileCommand(
            "Troika", "", "a@b.co", null, "", null,
            new Dictionary<DocumentType, string> { [DocumentType.Invoice] = "IN V" });

        new UpdateBusinessProfileValidator().Validate(command).IsValid.ShouldBeFalse();
    }
}
