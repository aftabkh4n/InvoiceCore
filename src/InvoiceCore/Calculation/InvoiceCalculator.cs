namespace InvoiceCore;

/// <summary>
/// Pure calculation engine. No I/O, no clock, no mutable state. Implements the
/// five-step pipeline from SPEC.md §4.3 and the inclusive-mode reconciliation
/// rule from §4.4.
/// </summary>
internal static class InvoiceCalculator
{
    /// <summary>Runs the full calculation pipeline for <paramref name="input"/>.</summary>
    internal static CalculationResult Compute(CalculationInput input)
    {
        var p = Money.GetDecimals(input.CurrencyCode);

        // Step 1: line totals (identical in both tax modes).
        //   lineGross_i    = Quantity_i * UnitPrice_i
        //   lineDiscount_i = Round(lineGross_i * LineDiscountPercent_i / 100, p)
        //   lineTotal_i    = Round(lineGross_i, p) - lineDiscount_i
        var lineTotals = new decimal[input.LineItems.Count];
        for (var i = 0; i < input.LineItems.Count; i++)
        {
            var line = input.LineItems[i];
            var lineGross = line.Quantity * line.UnitPrice;
            var lineDiscount = Money.Round(lineGross * line.DiscountPercent / 100m, p);
            lineTotals[i] = Money.Round(lineGross, p) - lineDiscount;
        }

        var rawSubtotal = lineTotals.Sum();

        // Step 2: normalise to a tax-exclusive base.
        //   Exclusive: Subtotal = rawSubtotal
        //   Inclusive: Subtotal = Round(rawSubtotal / (1 + R), p)
        //              where R = sum of all rate percentages / 100
        var R = input.TaxRates.Sum(r => r.Percentage) / 100m;
        var subtotal = input.TaxMode == TaxMode.Inclusive
            ? Money.Round(rawSubtotal / (1m + R), p)
            : rawSubtotal;

        // Step 3: invoice-level discount applied to the net subtotal.
        var discountAmount = Money.Round(subtotal * input.DiscountPercent / 100m, p);
        var taxableBase = subtotal - discountAmount;

        // Step 4: per-rate tax amounts, additive (never compounded).
        //   taxAmount_j = Round(taxableBase * Percentage_j / 100, p)
        var breakdown = input.TaxRates
            .Select(r => new TaxLineResult(
                r.Name,
                r.Percentage,
                Money.Round(taxableBase * r.Percentage / 100m, p)))
            .ToList();

        var taxAmount = breakdown.Sum(t => t.Amount);

        // Step 4.4: inclusive-mode reconciliation.
        //   When TaxMode == Inclusive and DiscountPercent == 0, the customer expects
        //   Total == rawSubtotal exactly. Rounding residuals from the division in
        //   step 2 and the per-rate rounding in step 4 can leave a one-minor-unit gap.
        //   residual = rawSubtotal - (Subtotal + TaxAmount)
        //   Add residual to the largest tax line (ties: first-declared order).
        if (input.TaxMode == TaxMode.Inclusive
            && input.DiscountPercent == 0m
            && breakdown.Count > 0)
        {
            var residual = rawSubtotal - (subtotal + taxAmount);
            if (residual != 0m)
            {
                var maxAmount = breakdown.Max(t => t.Amount);
                var idx = breakdown.FindIndex(t => t.Amount == maxAmount);
                breakdown[idx] = breakdown[idx] with { Amount = breakdown[idx].Amount + residual };
                taxAmount = breakdown.Sum(t => t.Amount);
            }
        }

        // Step 5: final total.
        var total = taxableBase + taxAmount;

        return new CalculationResult(
            lineTotals,
            subtotal,
            discountAmount,
            taxAmount,
            total,
            breakdown);
    }
}
