using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace InvoiceCore.Tests.Summary;

public sealed class SummaryTests
{
    private static readonly DateOnly Issued = new(2025, 1, 1);
    private static readonly DateOnly DueFuture = new(2099, 1, 1);
    private static readonly DateOnly DuePast = new(2024, 1, 1);

    private static FakeTimeProvider Clock2025 =>
        new(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

    private static Invoice MakeInvoice(
        string currency,
        decimal unitPrice,
        InvoiceStatus status = InvoiceStatus.Draft,
        DateOnly? dueDate = null,
        string? baseCurrency = null,
        decimal? exchangeRate = null)
    {
        var inv = new Invoice(
            [new LineItem { Description = "X", Quantity = 1m, UnitPrice = unitPrice }],
            [],
            $"INV-{Guid.NewGuid():N}",
            Issued,
            dueDate ?? DueFuture,
            new CustomerInfo { Name = "Acme" },
            currencyCode: currency,
            baseCurrencyCode: baseCurrency,
            exchangeRate: exchangeRate);

        if (status == InvoiceStatus.Sent || status == InvoiceStatus.Paid || status == InvoiceStatus.Cancelled)
            inv.UpdateStatus(InvoiceStatus.Sent);
        if (status == InvoiceStatus.Paid)
            inv.UpdateStatus(InvoiceStatus.Paid);
        if (status == InvoiceStatus.Cancelled)
            inv.UpdateStatus(InvoiceStatus.Cancelled);
        return inv;
    }

    // Empty collection → all counts zero, empty dictionaries, no rollup.
    [Fact]
    public void Empty_collection_produces_empty_summary()
    {
        var svc = new InvoiceService(Clock2025);
        var summary = svc.GetSummary([]);
        summary.Count.Should().Be(0);
        summary.CountByStatus.Should().BeEmpty();
        summary.ByCurrency.Should().BeEmpty();
        summary.Rollup.Should().BeNull();
    }

    // Mixed currencies → separate buckets, rollup null.
    [Fact]
    public void Mixed_currencies_produce_separate_buckets_and_no_rollup()
    {
        var svc = new InvoiceService(Clock2025);
        var usd = MakeInvoice("USD", 100m, InvoiceStatus.Sent);
        var eur = MakeInvoice("EUR", 200m, InvoiceStatus.Sent);

        var summary = svc.GetSummary([usd, eur]);

        summary.ByCurrency.Should().ContainKey("USD");
        summary.ByCurrency.Should().ContainKey("EUR");
        summary.ByCurrency["USD"].Total.Should().Be(100m);
        summary.ByCurrency["EUR"].Total.Should().Be(200m);
        summary.Rollup.Should().BeNull();
    }

    // Cancelled excluded from monetary totals but counted in CountByStatus.
    [Fact]
    public void Cancelled_excluded_from_monetary_totals_but_counted_in_status()
    {
        var svc = new InvoiceService(Clock2025);
        var sent = MakeInvoice("USD", 100m, InvoiceStatus.Sent);
        var cancelled = MakeInvoice("USD", 500m, InvoiceStatus.Cancelled);

        var summary = svc.GetSummary([sent, cancelled]);

        summary.Count.Should().Be(2);
        summary.CountByStatus.Should().ContainKey(InvoiceStatus.Cancelled).WhoseValue.Should().Be(1);
        summary.ByCurrency["USD"].Total.Should().Be(100m);  // 500 excluded
    }

    // Paid and outstanding buckets.
    [Fact]
    public void Paid_and_outstanding_buckets_are_computed_correctly()
    {
        var svc = new InvoiceService(Clock2025);
        var paid = MakeInvoice("USD", 80m, InvoiceStatus.Paid);
        var sent = MakeInvoice("USD", 120m, InvoiceStatus.Sent);

        var summary = svc.GetSummary([paid, sent]);

        summary.ByCurrency["USD"].Paid.Should().Be(80m);
        summary.ByCurrency["USD"].Outstanding.Should().Be(120m);
        summary.ByCurrency["USD"].Total.Should().Be(200m);
    }

    // Overdue bucket contains only sent+past-due invoices.
    [Fact]
    public void Overdue_bucket_reflects_clock_based_detection()
    {
        var svc = new InvoiceService(Clock2025);
        var overdue = MakeInvoice("USD", 50m, InvoiceStatus.Sent, dueDate: DuePast);
        var current = MakeInvoice("USD", 30m, InvoiceStatus.Sent, dueDate: DueFuture);

        var summary = svc.GetSummary([overdue, current]);

        summary.ByCurrency["USD"].Overdue.Should().Be(50m);
        summary.ByCurrency["USD"].Outstanding.Should().Be(80m); // both Sent
    }

    // Rollup when all invoices share the same BaseCurrencyCode + ExchangeRate.
    [Fact]
    public void Rollup_computed_when_all_invoices_have_same_base_currency()
    {
        var svc = new InvoiceService(Clock2025);
        var gbp1 = MakeInvoice("GBP", 100m, InvoiceStatus.Sent, baseCurrency: "USD", exchangeRate: 1.25m);
        var gbp2 = MakeInvoice("GBP", 200m, InvoiceStatus.Paid, baseCurrency: "USD", exchangeRate: 1.30m);

        var summary = svc.GetSummary([gbp1, gbp2]);

        summary.Rollup.Should().NotBeNull();
        summary.Rollup!.CurrencyCode.Should().Be("USD");
        summary.Rollup.Total.Should().Be(100m * 1.25m + 200m * 1.30m);  // 125 + 260 = 385
        summary.Rollup.Paid.Should().Be(200m * 1.30m);   // 260
        summary.Rollup.Outstanding.Should().Be(100m * 1.25m);  // 125
    }

    // Rollup is null when one invoice is missing BaseCurrencyCode.
    [Fact]
    public void Rollup_null_when_any_invoice_missing_base_currency()
    {
        var svc = new InvoiceService(Clock2025);
        var withBase = MakeInvoice("GBP", 100m, InvoiceStatus.Sent, baseCurrency: "USD", exchangeRate: 1.25m);
        var withoutBase = MakeInvoice("GBP", 100m, InvoiceStatus.Sent);

        var summary = svc.GetSummary([withBase, withoutBase]);
        summary.Rollup.Should().BeNull();
    }

    // Rollup is null when invoices have different base currencies.
    [Fact]
    public void Rollup_null_when_base_currencies_differ()
    {
        var svc = new InvoiceService(Clock2025);
        var usd = MakeInvoice("GBP", 100m, InvoiceStatus.Sent, baseCurrency: "USD", exchangeRate: 1.25m);
        var eur = MakeInvoice("GBP", 100m, InvoiceStatus.Sent, baseCurrency: "EUR", exchangeRate: 0.87m);

        var summary = svc.GetSummary([usd, eur]);
        summary.Rollup.Should().BeNull();
    }

    // Count reflects all invoices including cancelled.
    [Fact]
    public void Count_includes_all_statuses()
    {
        var svc = new InvoiceService(Clock2025);
        var invoices = new[]
        {
            MakeInvoice("USD", 10m, InvoiceStatus.Draft),
            MakeInvoice("USD", 10m, InvoiceStatus.Sent),
            MakeInvoice("USD", 10m, InvoiceStatus.Paid),
            MakeInvoice("USD", 10m, InvoiceStatus.Cancelled),
        };
        svc.GetSummary(invoices).Count.Should().Be(4);
    }
}
