using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Businesses.Settings;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Payments;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Businesses;

public sealed class PaymentAndMessagingSettingsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();

    private readonly IPaymentGatewayConfigRepository _gateways = Substitute.For<IPaymentGatewayConfigRepository>();
    private readonly IBusinessSettingsRepository _settings = Substitute.For<IBusinessSettingsRepository>();
    private readonly IBusinessSecretStore _secrets = Substitute.For<IBusinessSecretStore>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();

    public PaymentAndMessagingSettingsTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _business.BusinessId.Returns(BusinessId);
        _secrets.Protect(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
    }

    private UpdatePaymentSettingsHandler PaymentHandler() =>
        new(_tenant, _business, _gateways, _settings, _secrets, _activity, TimeProvider.System);

    [Fact]
    public async Task R33_new_paynow_key_is_encrypted_and_activity_never_contains_it()
    {
        PaymentGatewayConfig? added = null;
        _gateways.Add(Arg.Do<PaymentGatewayConfig>(c => added = c));
        string? summary = null;
        await _activity.AddAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Do<string>(s => summary = s), Arg.Any<CancellationToken>());

        var result = await PaymentHandler().Handle(new UpdatePaymentSettingsCommand("12345", "super-secret-key", false, "FNB 123"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.IntegrationKeyEncrypted.ShouldBe("enc:super-secret-key");
        summary.ShouldNotBeNull();
        summary.ShouldNotContain("super-secret-key");
    }

    [Fact]
    public async Task Blank_key_keeps_the_saved_key()
    {
        var existing = PaymentGatewayConfig.Create(TenantId, BusinessId, PaymentProvider.Paynow, "1", "enc:old", false, DateTimeOffset.UtcNow).Value;
        _gateways.GetAsync(BusinessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await PaymentHandler().Handle(new UpdatePaymentSettingsCommand("2", null, true, string.Empty), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        existing.IntegrationKeyEncrypted.ShouldBe("enc:old");
        existing.IntegrationId.ShouldBe("2");
        existing.IsLive.ShouldBeTrue();
    }

    [Fact]
    public async Task Paynow_id_without_a_key_is_rejected_on_first_save()
    {
        var result = await PaymentHandler().Handle(new UpdatePaymentSettingsCommand("12345", null, false, string.Empty), CancellationToken.None);

        result.Error.ShouldBe(UpdatePaymentSettingsHandler.KeyRequired);
    }

    [Fact]
    public async Task Query_reports_a_saved_key_without_returning_it()
    {
        var existing = PaymentGatewayConfig.Create(TenantId, BusinessId, PaymentProvider.Paynow, "1", "enc:old", true, DateTimeOffset.UtcNow).Value;
        _gateways.GetAsync(BusinessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await new GetPaymentSettingsHandler(_business, _gateways, _settings).Handle(new GetPaymentSettingsQuery(), CancellationToken.None);

        result.Value.PaynowKeySaved.ShouldBeTrue();
        result.Value.ToString().ShouldNotContain("enc:old");
    }

    [Fact]
    public async Task Test_connection_fails_when_not_configured_and_passes_when_key_decrypts()
    {
        var handler = new TestPaynowConnectionHandler(_business, _gateways, _secrets);
        (await handler.Handle(new TestPaynowConnectionCommand(), CancellationToken.None)).Error.ShouldBe(TestPaynowConnectionHandler.NotConfigured);

        var existing = PaymentGatewayConfig.Create(TenantId, BusinessId, PaymentProvider.Paynow, "1", "enc:old", false, DateTimeOffset.UtcNow).Value;
        _gateways.GetAsync(BusinessId, PaymentProvider.Paynow, Arg.Any<CancellationToken>()).Returns(existing);
        _secrets.Unprotect("enc:old").Returns("old");

        (await handler.Handle(new TestPaynowConnectionCommand(), CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Whatsapp_token_is_encrypted_kept_when_blank_and_removable()
    {
        var row = BusinessSettings.Create(TenantId, BusinessId, DateTimeOffset.UtcNow);
        _settings.GetAsync(BusinessId, Arg.Any<CancellationToken>()).Returns(row);
        var handler = new UpdateMessagingSettingsHandler(_tenant, _business, _settings, _secrets, _activity, TimeProvider.System);

        await handler.Handle(new UpdateMessagingSettingsCommand("Troika", "hi@troika.co.zw", "1234567", "tok", false), CancellationToken.None);
        row.WhatsAppTokenEncrypted.ShouldBe("enc:tok");

        await handler.Handle(new UpdateMessagingSettingsCommand("Troika", null, "1234567", null, false), CancellationToken.None);
        row.WhatsAppTokenEncrypted.ShouldBe("enc:tok");

        await handler.Handle(new UpdateMessagingSettingsCommand("Troika", null, "1234567", null, true), CancellationToken.None);
        row.WhatsAppTokenEncrypted.ShouldBeNull();
    }

    [Fact]
    public void Messaging_validator_rejects_bad_reply_to_and_phone_number_id()
    {
        var errors = new UpdateMessagingSettingsValidator()
            .Validate(new UpdateMessagingSettingsCommand("Troika", "nope", "abc", null, false))
            .Errors.Select(e => e.PropertyName);

        errors.ShouldBe(["EmailReplyTo", "WhatsAppPhoneNumberId"], ignoreOrder: true);
    }

    [Fact]
    public async Task R30_reminder_defaults_are_3_days_before_every_7_days_max_3()
    {
        _settings.GetAsync(BusinessId, Arg.Any<CancellationToken>()).Returns((BusinessSettings?)null);

        var result = await new GetReminderSettingsHandler(_business, _settings).Handle(new GetReminderSettingsQuery(), CancellationToken.None);

        result.Value.ShouldBe(new ReminderSettings(true, 3, true, 7, 3));
    }

    [Fact]
    public async Task R30_reminder_settings_are_saved_and_audited()
    {
        var row = BusinessSettings.Create(TenantId, BusinessId, DateTimeOffset.UtcNow);
        _settings.GetAsync(BusinessId, Arg.Any<CancellationToken>()).Returns(row);
        var handler = new UpdateReminderSettingsHandler(_tenant, _business, _settings, _activity, TimeProvider.System);

        var result = await handler.Handle(new UpdateReminderSettingsCommand(true, 5, false, 14, 2), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        row.ReminderDaysBeforeDue.ShouldBe(5);
        row.ReminderOnDueDate.ShouldBeFalse();
        row.ReminderRepeatEveryDays.ShouldBe(14);
        row.ReminderMaxAfterDue.ShouldBe(2);
        await _activity.Received(1).AddAsync(nameof(Business), BusinessId, "ReminderSettingsUpdated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void R30_reminder_validator_rejects_out_of_range_values()
    {
        new UpdateReminderSettingsValidator()
            .Validate(new UpdateReminderSettingsCommand(true, 99, true, 0, 50))
            .Errors.Count.ShouldBe(3);
    }
}
