using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Application.Payments;
using InvoiceBuilder.Domain.Payments;

namespace InvoiceBuilder.Application.Tests.Clients;

public sealed class StatementTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);

    private static StatementEntry Invoice(int month, int day, decimal amount, string currency = "USD", string number = "INV-0001") =>
        new(new DateOnly(2026, month, day), StatementEntryKind.Invoice, currency, number, "Invoice", amount);

    private static StatementEntry Credit(int month, int day, decimal amount, string currency = "USD") =>
        new(new DateOnly(2026, month, day), StatementEntryKind.CreditNote, currency, "CN-0001", "Credit note", amount);

    private static StatementEntry Paid(int month, int day, decimal amount, string currency = "USD") =>
        new(new DateOnly(2026, month, day), StatementEntryKind.Payment, currency, "PAY-1", "Payment", amount);

    [Fact]
    public void Opening_period_movements_and_closing_reconcile()
    {
        var sections = StatementCalculator.Build(
            [Invoice(9, 10, 500m), Paid(9, 20, 200m), Invoice(10, 5, 1000m, number: "INV-0002"), Credit(10, 8, 100m), Paid(10, 15, 400m), Invoice(11, 2, 999m)],
            From,
            To);

        var usd = sections.ShouldHaveSingleItem();
        usd.Opening.ShouldBe(300m);
        usd.Invoiced.ShouldBe(1000m);
        usd.Credited.ShouldBe(100m);
        usd.Paid.ShouldBe(400m);
        usd.Closing.ShouldBe(800m);
        usd.Closing.ShouldBe(usd.Opening + usd.Invoiced - usd.Credited - usd.Paid);
        usd.Lines.Select(l => l.Balance).ShouldBe([1300m, 1200m, 800m]);
        usd.Lines[0].Charge.ShouldBe(1000m);
        usd.Lines[1].Credit.ShouldBe(100m);
    }

    [Fact]
    public void R12_each_currency_gets_its_own_section_usd_first()
    {
        var sections = StatementCalculator.Build([Invoice(10, 2, 50m, "ZWG"), Invoice(10, 3, 20m)], From, To);

        sections.Select(s => s.Currency).ShouldBe(["USD", "ZWG"]);
        sections[1].Closing.ShouldBe(50m);
    }

    [Fact]
    public void Same_day_charges_come_before_payments()
    {
        var lines = StatementCalculator.Build([Paid(10, 5, 100m), Invoice(10, 5, 100m)], From, To).Single().Lines;

        lines.Select(l => l.Kind).ShouldBe([StatementEntryKind.Invoice, StatementEntryKind.Payment]);
        lines.Select(l => l.Balance).ShouldBe([100m, 0m]);
    }

    [Fact]
    public void Settled_currencies_with_no_movement_are_left_out_but_open_balances_stay()
    {
        var sections = StatementCalculator.Build([Invoice(8, 1, 100m), Paid(8, 2, 100m), Invoice(8, 3, 70m, "ZWG")], From, To);

        var zig = sections.ShouldHaveSingleItem();
        zig.Currency.ShouldBe("ZWG");
        zig.Opening.ShouldBe(70m);
        zig.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Overpayment_shows_as_a_negative_balance()
    {
        StatementCalculator.Build([Invoice(10, 1, 100m), Paid(10, 2, 150m)], From, To).Single().Closing.ShouldBe(-50m);
    }

    [Fact]
    public void Csv_has_opening_and_closing_rows_iso_dates_and_neutralised_formulas()
    {
        var statement = new ClientStatement(
            new StatementBusiness("Troika", null, "hi@troika.co.zw", null, "Harare", null, null),
            new StatementClient(Guid.NewGuid(), "=Evil Co", null, null),
            From,
            To,
            To,
            StatementCalculator.Build([new StatementEntry(new DateOnly(2026, 10, 5), StatementEntryKind.Invoice, "USD", "INV-0002", "=HYPERLINK(\"x\")", 1234.5m)], From, To),
            new Dictionary<string, string>());

        var lines = StatementCsv.Build(statement).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].ShouldBe("Currency,Date,Type,Reference,Details,Charge,Payment or credit,Balance");
        lines[1].ShouldBe("USD,2026-10-01,Opening balance,,,,,0.00");
        lines[2].ShouldStartWith("USD,2026-10-05,Invoice,INV-0002,\"'=HYPERLINK(");
        lines[2].ShouldEndWith(",1234.50,,1234.50");
        lines[3].ShouldBe("USD,2026-10-31,Closing balance,,,,,1234.50");
    }

    [Fact]
    public void File_name_is_safe_and_says_the_period()
    {
        var statement = new ClientStatement(
            new StatementBusiness("Troika", null, "hi@troika.co.zw", null, "Harare", null, null),
            new StatementClient(Guid.NewGuid(), "Harare Builders Co. (Pvt)", null, null),
            From,
            To,
            To,
            [],
            new Dictionary<string, string>());

        GetClientStatementFileHandler.StatementFileName(statement).ShouldBe("Statement-Harare-Builders-Co-Pvt-2026-10-01-to-2026-10-31");
    }

    [Fact]
    public void Payments_csv_uses_the_business_date_not_utc()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Harare");
        var late = new LedgerRow(Guid.NewGuid(), new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero), "Jane", "INV-0001", PaymentMethod.BankTransfer, 10m, "USD", PaymentStatus.Confirmed);

        var lines = PaymentLedgerCsv.Build([late], zone).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[1].ShouldBe("2026-11-01,Jane,INV-0001,Bank transfer,10.00,USD,Confirmed");
    }
}
