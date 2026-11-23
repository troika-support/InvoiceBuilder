using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Businesses.Settings;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests.Businesses;

public sealed class TaxSettingsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();
    private readonly Business _business = Business.Create(TenantId, "Troika", "", "a@b.co", "", DateTimeOffset.UtcNow);

    private UpdateTaxAndCurrencyHandler Handler()
    {
        var current = Substitute.For<ICurrentBusiness>();
        current.BusinessId.Returns(_business.Id);
        _businesses.GetByIdAsync(_business.Id, Arg.Any<CancellationToken>()).Returns(_business);
        return new UpdateTaxAndCurrencyHandler(current, _businesses, _activity, TimeProvider.System);
    }

    [Fact]
    public async Task R20_defaults_and_new_rate_are_saved_and_audited()
    {
        var existing = TaxRate.Create(TenantId, _business.Id, "Old VAT", TaxPercent.Create(14.5m).Value, new DateOnly(2024, 1, 1), null, DateTimeOffset.UtcNow);
        _businesses.ListTaxRatesAsync(_business.Id, Arg.Any<CancellationToken>()).Returns([existing]);
        TaxRate? added = null;
        _businesses.AddTaxRate(Arg.Do<TaxRate>(r => added = r));

        var result = await Handler().Handle(
            new UpdateTaxAndCurrencyCommand("ZWG", 30, 7, true, 40m, [new NewTaxRate("VAT", 15m, new DateOnly(2025, 1, 1), "T1")], [existing.Id]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _business.DefaultCurrency.ShouldBe(CurrencyCode.Zwg);
        _business.DefaultTermsDays.ShouldBe(30);
        _business.QuoteValidityDays.ShouldBe(7);
        _business.MinPartPaymentPercent.ShouldBe(40m);
        existing.IsArchived.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.Percent.Value.ShouldBe(15m);
        added.FiscalTaxId.ShouldBe("T1");
        await _activity.Received(1).AddAsync(nameof(Business), _business.Id, "TaxSettingsUpdated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void R11_unsupported_currency_and_out_of_range_values_are_invalid()
    {
        var errors = new UpdateTaxAndCurrencyValidator()
            .Validate(new UpdateTaxAndCurrencyCommand("EUR", -1, 0, true, 120m, [], []))
            .Errors.Select(e => e.PropertyName);

        errors.ShouldBe(["DefaultCurrency", "DefaultTermsDays", "QuoteValidityDays", "MinPartPaymentPercent"], ignoreOrder: true);
    }
}
