using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Domain.Documents;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Clients;

public sealed class ClientMetricsTests
{
    private static readonly DateOnly Today = new(2026, 6, 30);

    private static InvoiceFacts Invoice(
        string currency,
        decimal total,
        DocumentStatus status,
        decimal paid = 0,
        decimal credited = 0,
        DateOnly? issued = null,
        DateOnly? due = null,
        DateOnly? paidOn = null) =>
        new(currency, total, status, paid, credited, issued ?? new DateOnly(2026, 6, 1), due, paidOn);

    [Fact]
    public void R12_balances_are_kept_per_currency_and_never_summed()
    {
        var invoices = new[]
        {
            Invoice("USD", 100, DocumentStatus.Sent),
            Invoice("ZWG", 2500, DocumentStatus.Issued),
            Invoice("USD", 50, DocumentStatus.PartPaid, paid: 20),
        };

        var balances = ClientMetrics.BalancesByCurrency(invoices);

        balances.Count.ShouldBe(2);
        balances["USD"].ShouldBe(130m);
        balances["ZWG"].ShouldBe(2500m);
    }

    [Fact]
    public void R12_balance_ignores_drafts_paid_and_void_invoices_and_subtracts_credits()
    {
        var invoices = new[]
        {
            Invoice("USD", 100, DocumentStatus.Draft),
            Invoice("USD", 100, DocumentStatus.Paid, paid: 100),
            Invoice("USD", 100, DocumentStatus.Void),
            Invoice("USD", 80, DocumentStatus.Viewed, credited: 30),
        };

        ClientMetrics.BalancesByCurrency(invoices)["USD"].ShouldBe(50m);
    }

    [Fact]
    public void R12_overdue_balance_only_counts_open_invoices_past_due_per_currency()
    {
        var invoices = new[]
        {
            Invoice("USD", 100, DocumentStatus.Sent, due: Today.AddDays(-1)),
            Invoice("USD", 40, DocumentStatus.Sent, due: Today),
            Invoice("ZWG", 900, DocumentStatus.Issued, due: Today.AddDays(-10)),
        };

        var overdue = ClientMetrics.OverdueByCurrency(invoices, Today);

        overdue["USD"].ShouldBe(100m);
        overdue["ZWG"].ShouldBe(900m);
    }

    [Fact]
    public void Average_days_to_pay_uses_paid_invoices_only()
    {
        var invoices = new[]
        {
            Invoice("USD", 100, DocumentStatus.Paid, paid: 100, issued: new DateOnly(2026, 5, 1), paidOn: new DateOnly(2026, 5, 11)),
            Invoice("USD", 100, DocumentStatus.Paid, paid: 100, issued: new DateOnly(2026, 5, 1), paidOn: new DateOnly(2026, 5, 21)),
            Invoice("USD", 100, DocumentStatus.PartPaid, paid: 50, issued: new DateOnly(2026, 1, 1), paidOn: new DateOnly(2026, 6, 1)),
            Invoice("USD", 100, DocumentStatus.Sent, issued: new DateOnly(2026, 1, 1)),
        };

        ClientMetrics.AverageDaysToPay(invoices).ShouldBe(15d);
    }

    [Fact]
    public void Average_days_to_pay_is_null_when_nothing_is_paid()
    {
        ClientMetrics.AverageDaysToPay([Invoice("USD", 100, DocumentStatus.Sent)]).ShouldBeNull();
    }

    [Fact]
    public void Billed_this_year_is_per_currency_and_counts_issued_invoices_only()
    {
        var invoices = new[]
        {
            Invoice("USD", 100, DocumentStatus.Paid, issued: new DateOnly(2026, 2, 1)),
            Invoice("USD", 60, DocumentStatus.Sent, issued: new DateOnly(2026, 3, 1)),
            Invoice("USD", 500, DocumentStatus.Sent, issued: new DateOnly(2025, 12, 31)),
            Invoice("USD", 500, DocumentStatus.Draft, issued: new DateOnly(2026, 3, 1)),
            Invoice("ZWG", 900, DocumentStatus.Issued, issued: new DateOnly(2026, 4, 1)),
        };

        var billed = ClientMetrics.BilledInYear(invoices, 2026);

        billed["USD"].ShouldBe(160m);
        billed["ZWG"].ShouldBe(900m);
    }
}
