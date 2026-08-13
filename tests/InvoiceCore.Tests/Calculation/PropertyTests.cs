using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace InvoiceCore.Tests.Calculation;

/// <summary>
/// Property-based tests for all seven invariants in SPEC.md §4.5.
/// Generators are written explicitly against the FsCheck 3.x Fluent API.
/// </summary>
public sealed class PropertyTests
{
    // -----------------------------------------------------------------------
    // Generators
    // -----------------------------------------------------------------------

    // Percentage in [0.00, 100.00] (integer basis → no floating-point drift)
    private static Gen<decimal> GenPercent()
        => Gen.Choose(0, 10000).Select(n => n / 100m);

    // Positive quantity in [0.01, 99.99]
    private static Gen<decimal> GenPositiveQty()
        => Gen.Choose(1, 9999).Select(n => n / 100m);

    // Non-negative price in [0.00, 9999.99]
    private static Gen<decimal> GenPrice()
        => Gen.Choose(0, 999999).Select(n => n / 100m);

    private static Gen<TaxMode> GenTaxMode()
        => Gen.Elements(TaxMode.Exclusive, TaxMode.Inclusive);

    private static Gen<string> GenCurrency()
        => Gen.Elements("USD", "JPY", "KWD");

    private static Gen<LineItemInput> GenLineItem()
        => from qty in GenPositiveQty()
           from price in GenPrice()
           from disc in GenPercent()
           select new LineItemInput(qty, price, disc);

    private static Gen<TaxRateInput> GenTaxRate()
        => from pct in GenPercent()
           select new TaxRateInput("Tax", pct);

    private static Gen<CalculationInput> GenInput()
        => from mode in GenTaxMode()
           from currency in GenCurrency()
           from lineCount in Gen.Choose(0, 5)
           from lines in GenLineItem().ListOf(lineCount)
           from rateCount in Gen.Choose(0, 3)
           from rates in GenTaxRate().ListOf(rateCount)
           from invDisc in GenPercent()
           select new CalculationInput(lines, rates, invDisc, mode, currency);

    // Input constrained for invariant 5 (Inclusive, zero discount, at least one line)
    private static Gen<CalculationInput> GenInclusiveZeroDiscount()
        => from currency in GenCurrency()
           from lineCount in Gen.Choose(1, 5)
           from lines in GenLineItem().ListOf(lineCount)
           from rateCount in Gen.Choose(1, 3)     // need at least one rate
           from rates in GenTaxRate().ListOf(rateCount)
           select new CalculationInput(lines, rates, 0m, TaxMode.Inclusive, currency);

    // Input for invariant 6 (zero tax rates)
    private static Gen<CalculationInput> GenZeroTaxRates()
        => from mode in GenTaxMode()
           from currency in GenCurrency()
           from lineCount in Gen.Choose(0, 5)
           from lines in GenLineItem().ListOf(lineCount)
           from invDisc in GenPercent()
           select new CalculationInput(lines, [], invDisc, mode, currency);

    private static Arbitrary<CalculationInput> ArbInput()
        => Arb.From(GenInput());

    // -----------------------------------------------------------------------
    // Invariant 1 — Total == Subtotal - DiscountAmount + TaxAmount (always)
    // -----------------------------------------------------------------------
    [Property(MaxTest = 1000)]
    public Property Invariant1_total_accounting_identity()
        => Prop.ForAll(ArbInput(), input =>
        {
            var r = InvoiceCalculator.Compute(input);
            return r.Total == r.Subtotal - r.DiscountAmount + r.TaxAmount;
        });

    // -----------------------------------------------------------------------
    // Invariant 2 — TaxAmount == TaxBreakdown.Sum(t => t.Amount) (always)
    // -----------------------------------------------------------------------
    [Property(MaxTest = 1000)]
    public Property Invariant2_tax_amount_matches_breakdown_sum()
        => Prop.ForAll(ArbInput(), input =>
        {
            var r = InvoiceCalculator.Compute(input);
            return r.TaxAmount == r.TaxBreakdown.Sum(t => t.Amount);
        });

    // -----------------------------------------------------------------------
    // Invariant 3 — every decimal rounded to p; callers never need to round
    // -----------------------------------------------------------------------
    [Property(MaxTest = 1000)]
    public Property Invariant3_all_decimals_already_rounded()
        => Prop.ForAll(ArbInput(), input =>
        {
            var p = Money.GetDecimals(input.CurrencyCode);
            var r = InvoiceCalculator.Compute(input);

            var allDecimals = r.LineTotals
                .Concat([r.Subtotal, r.DiscountAmount, r.TaxAmount, r.Total])
                .Concat(r.TaxBreakdown.Select(t => t.Amount));

            return allDecimals.All(v => Money.Round(v, p) == v);
        });

    // -----------------------------------------------------------------------
    // Invariant 4 — empty LineItems → all zeros
    // -----------------------------------------------------------------------
    [Fact]
    public void Invariant4_empty_line_items_produce_all_zeros()
    {
        foreach (var (currency, mode, disc) in new[]
        {
            ("USD", TaxMode.Exclusive, 0m),
            ("USD", TaxMode.Inclusive, 0m),
            ("JPY", TaxMode.Exclusive, 10m),
            ("KWD", TaxMode.Inclusive, 5m),
        })
        {
            var input = new CalculationInput(
                [],
                [new TaxRateInput("VAT", 20m)],
                disc,
                mode,
                currency);

            var r = InvoiceCalculator.Compute(input);

            Assert.Equal(0m, r.Subtotal);
            Assert.Equal(0m, r.DiscountAmount);
            Assert.Equal(0m, r.TaxAmount);
            Assert.Equal(0m, r.Total);
        }
    }

    // -----------------------------------------------------------------------
    // Invariant 5 — Inclusive, zero discount: Total == sum of LineTotals
    // -----------------------------------------------------------------------
    [Property(MaxTest = 1000)]
    public Property Invariant5_inclusive_zero_discount_total_equals_line_sum()
        => Prop.ForAll(Arb.From(GenInclusiveZeroDiscount()), input =>
        {
            var r = InvoiceCalculator.Compute(input);
            return r.Total == r.LineTotals.Sum();
        });

    // -----------------------------------------------------------------------
    // Invariant 6 — zero tax rates: TaxAmount == 0 and Total == taxableBase
    // -----------------------------------------------------------------------
    [Property(MaxTest = 1000)]
    public Property Invariant6_zero_tax_rates_no_tax()
        => Prop.ForAll(Arb.From(GenZeroTaxRates()), input =>
        {
            var r = InvoiceCalculator.Compute(input);
            var taxableBase = r.Subtotal - r.DiscountAmount;
            return r.TaxAmount == 0m && r.Total == taxableBase;
        });

    // -----------------------------------------------------------------------
    // Invariant 7 — calculation is pure: same result on second call
    // Note: record == on IReadOnlyList<T> fields is reference equality, so we
    // compare each scalar field and use SequenceEqual for the collections.
    // -----------------------------------------------------------------------
    [Property(MaxTest = 500)]
    public Property Invariant7_calculation_is_pure()
        => Prop.ForAll(ArbInput(), input =>
        {
            var r1 = InvoiceCalculator.Compute(input);
            var r2 = InvoiceCalculator.Compute(input);
            return r1.Subtotal == r2.Subtotal
                && r1.DiscountAmount == r2.DiscountAmount
                && r1.TaxAmount == r2.TaxAmount
                && r1.Total == r2.Total
                && r1.LineTotals.SequenceEqual(r2.LineTotals)
                && r1.TaxBreakdown.Select(t => (t.Name, t.Percentage, t.Amount))
                     .SequenceEqual(r2.TaxBreakdown.Select(t => (t.Name, t.Percentage, t.Amount)));
        });
}
