using Xunit;

namespace InvoiceCore.Tests.Calculation;

/// <summary>
/// Targeted edge-case tests for rounding behaviour.
/// Each test names the SPEC.md rule it exercises.
/// </summary>
public sealed class RoundingTests
{
    // -----------------------------------------------------------------------
    // Money.Round: direct midpoint tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Round_0_005_at_2dp_rounds_up_AwayFromZero_not_banker()
    {
        // 0.005 is the midpoint at 2dp.
        // AwayFromZero → 0.01.  Banker's rounding → 0.00 (ties to even).
        Assert.Equal(0.01m, Money.Round(0.005m, 2));
    }

    [Fact]
    public void Round_0_5_at_0dp_rounds_up_AwayFromZero_not_banker()
    {
        // 0.5 is the midpoint at 0dp.
        // AwayFromZero → 1.  Banker's rounding → 0 (ties to even).
        Assert.Equal(1m, Money.Round(0.5m, 0));
    }

    [Fact]
    public void Round_negative_midpoint_rounds_away_from_zero()
    {
        // "Away from zero" means -0.005 → -0.01 (magnitude increases)
        Assert.Equal(-0.01m, Money.Round(-0.005m, 2));
    }

    [Fact]
    public void Round_3dp_KWD_midpoint()
    {
        // 0.0005 at 3dp: AwayFromZero → 0.001
        Assert.Equal(0.001m, Money.Round(0.0005m, 3));
    }

    // -----------------------------------------------------------------------
    // Money.GetDecimals: currency precision table
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("GBP", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("CLP", 0)]
    [InlineData("BHD", 3)]
    [InlineData("KWD", 3)]
    [InlineData("OMR", 3)]
    [InlineData("usd", 2)]   // case-insensitive
    [InlineData("jpy", 0)]
    [InlineData("kwd", 3)]
    [InlineData("XYZ", 2)]   // unknown → default 2
    [InlineData("",    2)]   // empty → default 2
    [InlineData(null,  2)]   // null  → default 2, no throw
    public void GetDecimals_returns_correct_precision(string? code, int expected)
    {
        Assert.Equal(expected, Money.GetDecimals(code));
    }

    // -----------------------------------------------------------------------
    // InvoiceCalculator: calculation-level rounding edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public void Tax_0_005_at_2dp_rounds_up_not_banker()
    {
        // price=0.10, qty=1, tax=5% → taxAmount=Round(0.10*0.05,2)=Round(0.005,2)=0.01
        // This catches the common off-by-one from using decimal.Round without rounding mode.
        var input = new CalculationInput(
            [new LineItemInput(1m, 0.10m, 0m)],
            [new TaxRateInput("Tax", 5m)],
            0m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0.10m, r.Subtotal);
        Assert.Equal(0.01m, r.TaxAmount);
        Assert.Equal(0.11m, r.Total);
    }

    [Fact]
    public void JPY_tax_midpoint_0_5_rounds_up_AwayFromZero()
    {
        // price=995 JPY, 10% tax → taxAmount=Round(99.5,0)=100 (not 99 with banker's)
        var input = new CalculationInput(
            [new LineItemInput(1m, 995m, 0m)],
            [new TaxRateInput("CT", 10m)],
            0m, TaxMode.Exclusive, "JPY");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(100m, r.TaxAmount);
        Assert.Equal(1095m, r.Total);
    }

    [Fact]
    public void KWD_three_decimal_precision_maintained()
    {
        // price=6.667 KWD, 5% tax → taxAmount=Round(6.667*0.05,3)=Round(0.33335,3)
        // 4th decimal of 0.33335 is 3 < 5 → 0.333
        var input = new CalculationInput(
            [new LineItemInput(1m, 6.667m, 0m)],
            [new TaxRateInput("VAT", 5m)],
            0m, TaxMode.Exclusive, "KWD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(6.667m, r.Subtotal);
        Assert.Equal(0.333m, r.TaxAmount);
        Assert.Equal(7.000m, r.Total);
    }

    [Fact]
    public void Quantity_0_333_rounds_line_total_correctly()
    {
        // qty=0.333, price=1.00 → lineGross=0.333
        // lineTotal=Round(0.333,2)=0.33   [3rd decimal=3 < 5]
        var input = new CalculationInput(
            [new LineItemInput(0.333m, 1.00m, 0m)],
            [],
            0m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0.33m, r.LineTotals[0]);
        Assert.Equal(0.33m, r.Total);
    }

    [Fact]
    public void Unit_price_0_01_qty_3_sums_correctly()
    {
        // qty=3, price=0.01 → lineGross=0.03 → lineTotal=Round(0.03,2)=0.03
        // With 20% tax: taxAmount=Round(0.03*0.20,2)=Round(0.006,2)=0.01
        var input = new CalculationInput(
            [new LineItemInput(3m, 0.01m, 0m)],
            [new TaxRateInput("Tax", 20m)],
            0m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0.03m, r.LineTotals[0]);
        Assert.Equal(0.03m, r.Subtotal);
        Assert.Equal(0.01m, r.TaxAmount);  // Round(0.006,2)=0.01 AwayFromZero
        Assert.Equal(0.04m, r.Total);
    }

    [Fact]
    public void Hundred_percent_line_discount_produces_zero_line_total()
    {
        // lineGross=100, lineDisc=Round(100*100/100,2)=100 → lineTotal=0
        var input = new CalculationInput(
            [new LineItemInput(1m, 100.00m, 100m)],
            [new TaxRateInput("VAT", 20m)],
            0m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0.00m, r.LineTotals[0]);
        Assert.Equal(0.00m, r.Subtotal);
        Assert.Equal(0.00m, r.TaxAmount);
        Assert.Equal(0.00m, r.Total);
    }

    [Fact]
    public void Zero_percent_tax_rate_contributes_zero_to_tax_amount()
    {
        var input = new CalculationInput(
            [new LineItemInput(1m, 100.00m, 0m)],
            [new TaxRateInput("Tax", 0m)],
            0m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0.00m, r.TaxAmount);
        Assert.Single(r.TaxBreakdown);
        Assert.Equal(0.00m, r.TaxBreakdown[0].Amount);
        Assert.Equal(100.00m, r.Total);
    }

    [Fact]
    public void Empty_line_items_produce_all_zero_result()
    {
        var input = new CalculationInput(
            [],
            [new TaxRateInput("VAT", 20m)],
            10m, TaxMode.Exclusive, "USD");

        var r = InvoiceCalculator.Compute(input);

        Assert.Equal(0m, r.Subtotal);
        Assert.Equal(0m, r.DiscountAmount);
        Assert.Equal(0m, r.TaxAmount);
        Assert.Equal(0m, r.Total);
        Assert.Empty(r.LineTotals);
    }
}
