using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests;

/// <summary>
/// Regression guard: validates InvoiceCore's arithmetic against two published tax authority sources.
///
/// Sources:
///   HMRC VATREC12030 — https://www.gov.uk/hmrc-internal-manuals/vat-trader-records/vatrec12030
///   HMRC Notice 700 s17.5 (truncation concession) — referenced in VATREC12010
///   ATO GSTA 1999 s9-90 — https://classic.austlii.edu.au/au/legis/cth/consol_act/antsasta1999402/s9.90.html
///
/// These tests assert the CURRENT behaviour of InvoiceCore. Do not change them to match a
/// different rounding method without updating docs/TAX-CONFORMANCE.md and SPEC.md to match.
/// </summary>
public sealed class TaxAuthorityConformanceTests
{
    private static InvoiceService Svc() => new();

    private static Invoice SingleLine(string currency, decimal rate, decimal unitPrice) =>
        Svc().Create(new CreateInvoiceRequest
        {
            InvoiceNumber = "CONF",
            IssuedDate    = new DateOnly(2026, 1, 1),
            DueDate       = new DateOnly(2026, 2, 1),
            CurrencyCode  = currency,
            Customer      = new CustomerInfo { Name = "Test" },
            LineItems     = new List<LineItem> { new() { Description = "Item", Quantity = 1m, UnitPrice = unitPrice } },
            TaxRates      = new List<TaxRate>  { new() { Name = "Tax", Percentage = rate } },
        });

    private static Invoice MultiLine(
        string currency,
        decimal rate,
        decimal[] prices,
        TaxCalculationMethod method = TaxCalculationMethod.SubtotalFirst)
    {
        var items = new List<LineItem>();
        foreach (var p in prices)
            items.Add(new LineItem { Description = "Item", Quantity = 1m, UnitPrice = p });
        return Svc().Create(new CreateInvoiceRequest
        {
            InvoiceNumber        = "CONF",
            IssuedDate           = new DateOnly(2026, 1, 1),
            DueDate              = new DateOnly(2026, 2, 1),
            CurrencyCode         = currency,
            Customer             = new CustomerInfo { Name = "Test" },
            LineItems            = items,
            TaxRates             = new List<TaxRate> { new() { Name = "Tax", Percentage = rate } },
            TaxCalculationMethod = method,
        });
    }

    // -------------------------------------------------------------------------
    // HMRC UK 5% reduced rate — 0.5p midpoint cases
    //
    // Source: VATREC12030 — "If the VAT comes to 0.5 of one penny or more, it
    //   should be rounded up."
    //
    // These net values produce exactly 0.5p VAT at 5%:
    //   net × 0.05 = x.xx5 (half-penny exactly)
    // InvoiceCore applies AwayFromZero, which rounds up, matching the HMRC
    // standard rule. The HMRC optional truncation concession (Notice 700 s17.5)
    // would permit rounding down instead; InvoiceCore does not implement it.
    //
    // Column meaning: unitPrice, expectedTax, expectedTotal
    // Note: no divergence is possible at UK 20% standard rate on whole-penny
    // net values — 20% of any integer pence is never exactly 0.5p.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.10, 0.01, 0.11)]  // 0.10 × 0.05 = 0.005 — concession would give 0.00
    [InlineData(0.30, 0.02, 0.32)]  // 0.30 × 0.05 = 0.015 — concession would give 0.01
    [InlineData(0.90, 0.05, 0.95)]  // 0.90 × 0.05 = 0.045 — concession would give 0.04
    [InlineData(3.90, 0.20, 4.10)]  // 3.90 × 0.05 = 0.195 — concession would give 0.19
    public void HMRC_5pct_midpoint_matches_standard_half_up_rule(
        decimal unitPrice, decimal expectedTax, decimal expectedTotal)
    {
        var inv = SingleLine("GBP", 5m, unitPrice);
        inv.TaxAmount.Should().Be(expectedTax);
        inv.Total.Should().Be(expectedTotal);
    }

    // Non-midpoint cases at 5% — both HMRC standard and the concession agree.
    [Theory]
    [InlineData(1.00, 0.05, 1.05)]  // 1.00 × 0.05 = 0.050 — exact, no rounding needed
    [InlineData(0.23, 0.01, 0.24)]  // 0.23 × 0.05 = 0.0115 — third decimal 1, rounds down
    public void HMRC_5pct_non_midpoint_unambiguous(
        decimal unitPrice, decimal expectedTax, decimal expectedTotal)
    {
        var inv = SingleLine("GBP", 5m, unitPrice);
        inv.TaxAmount.Should().Be(expectedTax);
        inv.Total.Should().Be(expectedTotal);
    }

    // -------------------------------------------------------------------------
    // HMRC multi-line: subtotal-first vs per-line method divergence
    //
    // Source: VATREC12030 permits either method. InvoiceCore uses subtotal-first.
    // Three lines at £1.67 each, 20% VAT:
    //   subtotal-first: Round(£5.01 × 0.20) = Round(£1.002) = £1.00
    //   per-line:       3 × Round(£1.67 × 0.20) = 3 × £0.33 = £0.99
    // -------------------------------------------------------------------------

    [Fact]
    public void HMRC_20pct_multiline_SubtotalFirst_uses_aggregate_rounding()
    {
        var inv = MultiLine("GBP", 20m, [1.67m, 1.67m, 1.67m]);
        inv.Subtotal.Should().Be(5.01m);
        inv.TaxAmount.Should().Be(1.00m);  // Round(5.01 × 0.20) = Round(1.002) = 1.00
        inv.Total.Should().Be(6.01m);
    }

    [Fact]
    public void HMRC_20pct_multiline_PerLine_uses_per_line_rounding()
    {
        // VATREC12030 permits either method. PerLine gives the documented alternative result.
        // TaxAmount = 3 × Round(1.67 × 0.20) = 3 × 0.33 = 0.99 (not 1.00)
        var inv = MultiLine("GBP", 20m, [1.67m, 1.67m, 1.67m], TaxCalculationMethod.PerLine);
        inv.Subtotal.Should().Be(5.01m);
        inv.TaxAmount.Should().Be(0.99m);  // 3 × Round(1.67 × 0.20, 2) = 3 × 0.33
        inv.Total.Should().Be(6.00m);
    }

    // -------------------------------------------------------------------------
    // ATO Australia GST 10% — midpoint cases
    //
    // Source: GSTA 1999 s9-90 — "if the amount of GST on a taxable supply
    //   includes a fraction of a cent, the amount of GST is rounded to the
    //   nearest cent (rounding 0.5 cents upwards)."
    //
    // At 10%, net values that are multiples of A$0.05 but not A$0.10 produce
    // exactly 0.5c GST. AwayFromZero rounds up, matching the ATO rule.
    //
    // Column meaning: unitPrice (AUD), expectedGST, expectedTotal
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.05, 0.01, 0.06)]  // 0.05 × 0.10 = 0.005 — rounds up to 0.01
    [InlineData(0.25, 0.03, 0.28)]  // 0.25 × 0.10 = 0.025 — rounds up to 0.03
    [InlineData(0.55, 0.06, 0.61)]  // 0.55 × 0.10 = 0.055 — rounds up to 0.06
    [InlineData(1.10, 0.11, 1.21)]  // 1.10 × 0.10 = 0.110 — exact, no rounding needed
    public void ATO_10pct_matches_nearest_cent_half_up_rule(
        decimal unitPrice, decimal expectedGST, decimal expectedTotal)
    {
        var inv = SingleLine("AUD", 10m, unitPrice);
        inv.TaxAmount.Should().Be(expectedGST);
        inv.Total.Should().Be(expectedTotal);
    }

    // -------------------------------------------------------------------------
    // ATO two-method divergence: total-invoice rule vs taxable-supply rule
    //
    // Source: GSTA 1999 s9-90 permits both methods; buyer and supplier need not
    //   use the same one.
    //
    // 2 × A$0.05 at 10% GST:
    //   Total-invoice rule (InvoiceCore): Round(A$0.10 × 0.10) = Round(A$0.010) = A$0.01
    //   Taxable-supply rule (per-line):   2 × Round(A$0.05 × 0.10) = 2 × A$0.01 = A$0.02
    // -------------------------------------------------------------------------

    [Fact]
    public void ATO_10pct_two_supplies_SubtotalFirst_uses_total_invoice_rule()
    {
        var inv = MultiLine("AUD", 10m, [0.05m, 0.05m]);
        inv.Subtotal.Should().Be(0.10m);
        inv.TaxAmount.Should().Be(0.01m);  // total-invoice rule: Round(0.10 × 0.10) = 0.01
        inv.Total.Should().Be(0.11m);
    }

    [Fact]
    public void ATO_10pct_two_supplies_PerLine_uses_taxable_supply_rule()
    {
        // GSTA 1999 s9-90 permits the taxable-supply (per-line) method.
        // TaxAmount = 2 × Round(0.05 × 0.10) = 2 × Round(0.005) = 2 × 0.01 = 0.02
        var inv = MultiLine("AUD", 10m, [0.05m, 0.05m], TaxCalculationMethod.PerLine);
        inv.Subtotal.Should().Be(0.10m);
        inv.TaxAmount.Should().Be(0.02m);  // 2 × Round(0.05 × 0.10, 2) = 2 × 0.01
        inv.Total.Should().Be(0.12m);
    }
}
