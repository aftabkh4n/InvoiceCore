using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Calculation;

/// <summary>
/// Tests for TaxCalculationMethod.PerLine, covering:
///   - Construction guard (PerLine + Inclusive → NotSupportedException)
///   - Golden arithmetic cases (kills M20: wrong DiscountAmount; M21: collapsed to SubtotalFirst)
///   - Periodic residual congruence analysis (documents why PerLine + Inclusive is deferred)
///
/// Mutation targets:
///   M20 — PerLine: DiscountAmount = Round(Subtotal × pct) instead of SUM(lineDiscount_i)
///   M21 — PerLine: tax rounded once after summing, collapsing to SubtotalFirst behaviour
///   M22 — PerLine + Inclusive guard removed entirely
///   M23 — Guard moved from constructor to a property getter
/// </summary>
public sealed class PerLineCalculationTests
{
    private static readonly DateOnly IssueDate = new(2026, 1, 1);
    private static readonly DateOnly DueDate   = new(2026, 2, 1);
    private static readonly CustomerInfo Customer = new() { Name = "Test" };

    private static Invoice BuildPerLine(
        string currency,
        decimal[] unitPrices,
        decimal taxRate,
        decimal invoiceDiscountPct = 0m) =>
        new(
            lineItems: [..unitPrices.Select(p => new LineItem
            {
                Description = "Item", Quantity = 1m, UnitPrice = p,
            })],
            taxRates: [new TaxRate { Name = "Tax", Percentage = taxRate }],
            invoiceNumber: "PL-TEST",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            currencyCode: currency,
            discountPercent: invoiceDiscountPct,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

    // -----------------------------------------------------------------------
    // Construction guard — kills M22 (guard removed) and M23 (guard deferred)
    // -----------------------------------------------------------------------

    [Fact]
    public void PerLine_Inclusive_throws_NotSupportedException_at_construction()
    {
        // The exception must come from the constructor, not from a property getter.
        // M22 (guard removed): no exception → test fails.
        // M23 (guard in getter): constructor returns, Assert.Throws sees no exception → test fails.
        var act = () => new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [new TaxRate { Name = "VAT", Percentage = 20m }],
            invoiceNumber: "GUARD",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            taxMode: TaxMode.Inclusive,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        act.Should().ThrowExactly<NotSupportedException>()
            .WithMessage("*PerLine*Inclusive*");
    }

    [Fact]
    public void PerLine_Exclusive_does_not_throw_at_construction()
    {
        var act = () => BuildPerLine("USD", [10m], 20m);
        act.Should().NotThrow();
    }

    [Fact]
    public void SubtotalFirst_Inclusive_does_not_throw_at_construction()
    {
        var act = () => new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [new TaxRate { Name = "VAT", Percentage = 20m }],
            invoiceNumber: "OK",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            taxMode: TaxMode.Inclusive,
            taxCalculationMethod: TaxCalculationMethod.SubtotalFirst);

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Invariant 1 holds with PerLine-specific DiscountAmount
    //
    // Kills M20: if DiscountAmount = Round(Subtotal × pct) instead of SUM(lineDisc_i),
    //            the Total = Subtotal − DiscountAmount + TaxAmount invariant breaks
    //            AND the asserted DiscountAmount value is wrong.
    // -----------------------------------------------------------------------

    [Fact]
    public void PerLine_3lines_33pct_discount_10pct_tax_USD()
    {
        // Subtotal = $3.00
        // SubtotalFirst: DiscountAmount = Round(3.00 × 0.3333, 2) = $1.00
        // PerLine:       lineDiscount_i = Round(1.00 × 0.3333, 2) = $0.33 each
        //                DiscountAmount = $0.99  ← M20 would produce $1.00 → test kills M20
        //                lineTaxable_i  = $0.67
        //                lineTax_i      = Round(0.67 × 0.10, 2) = $0.07
        //                TaxAmount      = $0.21  ← M21 would produce $0.20 → test kills M21
        //                Total          = $2.22
        var inv = BuildPerLine("USD", [1.00m, 1.00m, 1.00m], 10m, invoiceDiscountPct: 33.33m);

        inv.Subtotal.Should().Be(3.00m);
        inv.DiscountAmount.Should().Be(0.99m);       // kills M20
        inv.TaxAmount.Should().Be(0.21m);            // kills M21 (SubtotalFirst gives 0.20)
        inv.Total.Should().Be(2.22m);
        (inv.Subtotal - inv.DiscountAmount + inv.TaxAmount).Should().Be(inv.Total); // invariant 1
    }

    // -----------------------------------------------------------------------
    // Additional M20 kills across all three currency precisions.
    //
    // These demonstrate that SUM(lineDiscount_i) diverges from
    // Round(Subtotal × pct / 100, p) regardless of decimal precision,
    // and that the divergence can exceed 1 minor unit (USD 7-line case).
    //
    // Hand-calculated expected values verified by arithmetic script before writing.
    // -----------------------------------------------------------------------

    [Fact]
    public void PerLine_3lines_midpoint_discount_JPY()
    {
        // JPY: p=0 — rounding to the nearest yen.
        // 3 lines × ¥10, 5% invoice discount.
        //
        // Per-line:       lineDiscount_i = Round(10 × 5/100, 0) = Round(0.5, 0) = ¥1 (AwayFromZero)
        //                 DiscountAmount = 3 × ¥1 = ¥3
        // SubtotalFirst:  Round(30 × 5/100, 0) = Round(1.5, 0) = ¥2  ← M20 would produce this
        // Divergence:     1 minor unit
        //
        // With 10% tax:   lineTaxable_i = ¥9, lineTax_i = Round(9×0.10, 0) = Round(0.9,0) = ¥1
        //                 TaxAmount = ¥3, Total = 30 − 3 + 3 = ¥30
        var inv = new Invoice(
            lineItems: [
                new LineItem { Description = "A", Quantity = 1m, UnitPrice = 10m },
                new LineItem { Description = "B", Quantity = 1m, UnitPrice = 10m },
                new LineItem { Description = "C", Quantity = 1m, UnitPrice = 10m },
            ],
            taxRates: [new TaxRate { Name = "Tax", Percentage = 10m }],
            invoiceNumber: "JPY-DISC",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            currencyCode: "JPY",
            discountPercent: 5m,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        inv.Subtotal.Should().Be(30m);
        inv.DiscountAmount.Should().Be(3m);    // kills M20 (SubtotalFirst / M20 gives ¥2)
        inv.TaxAmount.Should().Be(3m);
        inv.Total.Should().Be(30m);
        (inv.Subtotal - inv.DiscountAmount + inv.TaxAmount).Should().Be(inv.Total);
    }

    [Fact]
    public void PerLine_3lines_midpoint_discount_KWD()
    {
        // KWD: p=3 — rounding to the nearest fils (1/1000 KWD).
        // 3 lines × KWD 0.333, 10% invoice discount.
        //
        // Per-line:       lineDiscount_i = Round(0.333 × 10/100, 3) = Round(0.0333, 3) = 0.033
        //                 DiscountAmount = 3 × 0.033 = 0.099
        // SubtotalFirst:  Round(0.999 × 10/100, 3) = Round(0.0999, 3) = 0.100  ← M20 would produce this
        // Divergence:     1 minor unit (1 fil)
        //
        // With 5% tax:    lineTaxable_i = 0.300, lineTax_i = Round(0.300 × 0.05, 3) = 0.015
        //                 TaxAmount = 0.045, Total = 0.999 − 0.099 + 0.045 = 0.945
        var inv = new Invoice(
            lineItems: [
                new LineItem { Description = "A", Quantity = 1m, UnitPrice = 0.333m },
                new LineItem { Description = "B", Quantity = 1m, UnitPrice = 0.333m },
                new LineItem { Description = "C", Quantity = 1m, UnitPrice = 0.333m },
            ],
            taxRates: [new TaxRate { Name = "Tax", Percentage = 5m }],
            invoiceNumber: "KWD-DISC",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            currencyCode: "KWD",
            discountPercent: 10m,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        inv.Subtotal.Should().Be(0.999m);
        inv.DiscountAmount.Should().Be(0.099m);    // kills M20 (SubtotalFirst / M20 gives 0.100)
        inv.TaxAmount.Should().Be(0.045m);
        inv.Total.Should().Be(0.945m);
        (inv.Subtotal - inv.DiscountAmount + inv.TaxAmount).Should().Be(inv.Total);
    }

    [Fact]
    public void PerLine_7lines_33pct_discount_10pct_tax_USD_divergence_is_2_minor_units()
    {
        // USD: p=2 — 7 lines show the divergence can exceed 1 minor unit.
        // 7 lines × $1.00, 33.33% invoice discount.
        //
        // Per-line:       lineDiscount_i = Round(1.00 × 33.33/100, 2) = Round(0.3333, 2) = $0.33
        //                 DiscountAmount = 7 × $0.33 = $2.31
        // SubtotalFirst:  Round(7.00 × 33.33/100, 2) = Round(2.3331, 2) = $2.33  ← M20 would produce this
        // Divergence:     2 minor units ($0.02)
        //
        // With 10% tax:   lineTaxable_i = $0.67, lineTax_i = Round(0.67 × 0.10, 2) = $0.07
        //                 TaxAmount = 7 × $0.07 = $0.49
        //                 Total = 7.00 − 2.31 + 0.49 = $5.18
        var inv = BuildPerLine("USD", [1m, 1m, 1m, 1m, 1m, 1m, 1m], 10m, invoiceDiscountPct: 33.33m);

        inv.Subtotal.Should().Be(7.00m);
        inv.DiscountAmount.Should().Be(2.31m);     // kills M20 (SubtotalFirst / M20 gives $2.33)
        inv.TaxAmount.Should().Be(0.49m);
        inv.Total.Should().Be(5.18m);
        (inv.Subtotal - inv.DiscountAmount + inv.TaxAmount).Should().Be(inv.Total);
    }

    [Fact]
    public void PerLine_TaxCalculationMethod_property_is_PerLine()
    {
        var inv = BuildPerLine("USD", [10m], 20m);
        inv.TaxCalculationMethod.Should().Be(TaxCalculationMethod.PerLine);
    }

    [Fact]
    public void SubtotalFirst_TaxCalculationMethod_property_is_SubtotalFirst()
    {
        var inv = new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [new TaxRate { Name = "VAT", Percentage = 20m }],
            invoiceNumber: "SF",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer);

        inv.TaxCalculationMethod.Should().Be(TaxCalculationMethod.SubtotalFirst);
    }

    // -----------------------------------------------------------------------
    // No-discount PerLine produces same Subtotal, different TaxAmount when
    // per-line rounding diverges from aggregate rounding.
    //
    // Kills M21: collapsing per-line to aggregate would give TaxAmount = $1.00.
    // -----------------------------------------------------------------------

    [Fact]
    public void PerLine_no_discount_same_Subtotal_as_SubtotalFirst()
    {
        // 3 × $1.67 USD at 20%: per-line gives TaxAmount = 3 × Round(1.67 × 0.20) = 3 × 0.33 = 0.99
        // SubtotalFirst gives Round(5.01 × 0.20) = Round(1.002) = 1.00
        var inv = BuildPerLine("USD", [1.67m, 1.67m, 1.67m], 20m);

        inv.Subtotal.Should().Be(5.01m);
        inv.DiscountAmount.Should().Be(0.00m);
        inv.TaxAmount.Should().Be(0.99m);            // kills M21
        inv.Total.Should().Be(6.00m);
        (inv.Subtotal - inv.DiscountAmount + inv.TaxAmount).Should().Be(inv.Total);
    }

    [Fact]
    public void PerLine_multi_rate_accumulates_per_rate_per_line()
    {
        // 2 lines × $10.00 USD, rates 10% + 5%, no discount.
        // Per line: Tax_10%_i = Round(10.00 × 0.10, 2) = 1.00
        //           Tax_5%_i  = Round(10.00 × 0.05, 2) = 0.50
        // Total:    Tax_10% = 2 × 1.00 = 2.00
        //           Tax_5%  = 2 × 0.50 = 1.00
        //           TaxAmount = 3.00
        var inv = new Invoice(
            lineItems: [
                new LineItem { Description = "A", Quantity = 1m, UnitPrice = 10m },
                new LineItem { Description = "B", Quantity = 1m, UnitPrice = 10m },
            ],
            taxRates: [
                new TaxRate { Name = "VAT", Percentage = 10m },
                new TaxRate { Name = "GST", Percentage = 5m },
            ],
            invoiceNumber: "MR",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        inv.Subtotal.Should().Be(20.00m);
        inv.TaxBreakdown[0].Amount.Should().Be(2.00m);
        inv.TaxBreakdown[1].Amount.Should().Be(1.00m);
        inv.TaxAmount.Should().Be(3.00m);
        inv.Total.Should().Be(23.00m);
    }

    [Fact]
    public void PerLine_empty_lines_produces_all_zeros()
    {
        var inv = new Invoice(
            lineItems: [],
            taxRates: [new TaxRate { Name = "VAT", Percentage = 20m }],
            invoiceNumber: "EMPTY",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        inv.Subtotal.Should().Be(0m);
        inv.DiscountAmount.Should().Be(0m);
        inv.TaxAmount.Should().Be(0m);
        inv.Total.Should().Be(0m);
    }

    [Fact]
    public void PerLine_zero_tax_rates_gives_no_tax()
    {
        var inv = new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 50m }],
            taxRates: [],
            invoiceNumber: "ZT",
            issuedDate: IssueDate,
            dueDate: DueDate,
            customer: Customer,
            taxCalculationMethod: TaxCalculationMethod.PerLine);

        inv.TaxAmount.Should().Be(0m);
        inv.Total.Should().Be(inv.Subtotal - inv.DiscountAmount);
    }

    // -----------------------------------------------------------------------
    // Periodic residual congruence — encodes the TAX-CONFORMANCE.md finding.
    //
    // These tests document the mathematical reason PerLine + Inclusive is
    // deferred. If someone later implements PerLine + Inclusive, they must
    // account for the multi-unit residuals these tests demonstrate.
    // -----------------------------------------------------------------------

    /// <summary>
    /// For 10% rate, r_i = lineInclusive - lineExclusive - lineTax = -1 (minor unit) exactly
    /// when lineTotal_cents ≡ 5 (mod 11). Verified for x in [1, 110].
    /// </summary>
    [Fact]
    public void PerLine_residual_congruence_10pct_USD()
    {
        const int p = 2;
        var R = 0.10m;
        var oneR = 1m + R;
        var expectedBad = new HashSet<int>(); // x mod 11 == 5
        for (var x = 1; x <= 110; x++)
            if (x % 11 == 5) expectedBad.Add(x);

        for (var xCents = 1; xCents <= 110; xCents++)
        {
            var lineInclusive = (decimal)xCents / 100m;
            var lineExclusive = Money.Round(lineInclusive / oneR, p);
            var lineTax       = Money.Round(lineExclusive * R, p);
            var rI = lineInclusive - lineExclusive - lineTax;

            if (expectedBad.Contains(xCents))
                rI.Should().Be(-0.01m, $"r_i must be -1¢ for ${lineInclusive:0.00} (x={xCents}, x mod 11={xCents % 11})");
            else
                rI.Should().Be(0m, $"r_i must be 0 for ${lineInclusive:0.00} (x={xCents})");
        }
    }

    /// <summary>
    /// For 20% rate, r_i = -1 (minor unit) when lineTotal_pence ≡ 3 (mod 6). Verified for x in [1, 60].
    /// Common affected prices: £0.03, £0.09, …, £0.99, £3.99, £9.99 …
    /// </summary>
    [Fact]
    public void PerLine_residual_congruence_20pct_GBP()
    {
        const int p = 2;
        var R = 0.20m;
        var oneR = 1m + R;

        for (var xPence = 1; xPence <= 60; xPence++)
        {
            var lineInclusive = (decimal)xPence / 100m;
            var lineExclusive = Money.Round(lineInclusive / oneR, p);
            var lineTax       = Money.Round(lineExclusive * R, p);
            var rI = lineInclusive - lineExclusive - lineTax;

            if (xPence % 6 == 3)
                rI.Should().Be(-0.01m, $"r_i must be -1p for £{lineInclusive:0.00} (x={xPence}, x mod 6={xPence % 6})");
            else
                rI.Should().Be(0m, $"r_i must be 0 for £{lineInclusive:0.00} (x={xPence})");
        }
    }

    /// <summary>
    /// The 7 × £3.99 GBP at 20% VAT worst-case from TAX-CONFORMANCE.md.
    /// Each line produces r_i = -1p, so the aggregate residual is -7p.
    /// This is the primary evidence that §4.4 (±1 unit) cannot cover PerLine + Inclusive.
    /// </summary>
    [Fact]
    public void PerLine_Inclusive_7x_GBP399_residual_is_7_minor_units()
    {
        // Compute directly without Invoice (PerLine + Inclusive throws at construction).
        const int p = 2;
        var R = 0.20m;
        var lineInclusive = 3.99m;
        var lineExclusive = Money.Round(lineInclusive / (1m + R), p); // 3.33
        var lineTax       = Money.Round(lineExclusive * R, p);        // 0.67
        var rI = lineInclusive - lineExclusive - lineTax;             // -0.01

        lineExclusive.Should().Be(3.33m);
        lineTax.Should().Be(0.67m);
        rI.Should().Be(-0.01m, "single line residual must be -1p");

        const int n = 7;
        var rawSubtotal = n * lineInclusive;           // 27.93
        var subtotal    = n * lineExclusive;           // 23.31
        var taxAmount   = n * lineTax;                 //  4.69
        var total       = subtotal + taxAmount;        // 28.00
        var residual    = rawSubtotal - total;         // -0.07

        rawSubtotal.Should().Be(27.93m);
        subtotal.Should().Be(23.31m);
        taxAmount.Should().Be(4.69m);
        total.Should().Be(28.00m);
        residual.Should().Be(-0.07m, "aggregate residual must be -7p (7 × -1p)");

        // The §4.4 fix would set TaxBreakdown[20%].Amount = 4.69 + (-0.07) = 4.62,
        // implying 4.62 / 23.31 ≈ 19.82% effective rate — a distorted filing figure.
        var adjustedTax = taxAmount + residual;
        adjustedTax.Should().Be(4.62m);
        // 4.62 / 23.31 ≠ 20%; the breakdown would misrepresent the VAT rate.
        (adjustedTax / subtotal).Should().NotBe(R, "adjusted tax misrepresents the 20% rate");
    }

    [Fact]
    public void PerLine_Inclusive_3x_USD093_residual_is_3_minor_units()
    {
        const int p = 2;
        var R = 0.10m;
        var lineInclusive = 0.93m;                     // 93 mod 11 = 5 → r_i = -1¢
        var lineExclusive = Money.Round(lineInclusive / (1m + R), p); // 0.85
        var lineTax       = Money.Round(lineExclusive * R, p);        // 0.09

        lineExclusive.Should().Be(0.85m);
        lineTax.Should().Be(0.09m);
        (lineInclusive - lineExclusive - lineTax).Should().Be(-0.01m);

        var residual3 = 3 * (lineInclusive - lineExclusive - lineTax);
        residual3.Should().Be(-0.03m, "3-line residual must be -3¢");

        var residual7 = 7 * (lineInclusive - lineExclusive - lineTax);
        residual7.Should().Be(-0.07m, "7-line residual must be -7¢");
    }
}
