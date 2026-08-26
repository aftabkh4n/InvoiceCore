namespace InvoiceCore;

/// <summary>
/// Pure calculation engine. No I/O, no clock, no mutable state. Implements a
/// five-step pipeline: line totals, subtotal normalisation, invoice discount,
/// per-rate tax, and inclusive-mode reconciliation. Supports both
/// <see cref="TaxCalculationMethod.SubtotalFirst"/> and <see cref="TaxCalculationMethod.PerLine"/>.
/// </summary>
internal static class InvoiceCalculator
{
    /// <summary>Runs the full calculation pipeline for <paramref name="input"/>.</summary>
    internal static CalculationResult Compute(CalculationInput input)
    {
        var p = Money.GetDecimals(input.CurrencyCode);

        // Step 1: line totals (identical in both calculation methods and both tax modes).
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

        decimal discountAmount;
        decimal taxableBase;
        List<TaxLineResult> breakdown;
        decimal taxAmount;

        if (input.TaxCalculationMethod == TaxCalculationMethod.PerLine)
        {
            // Steps 3-4 (PerLine): per-line invoice discount, then per-line per-rate tax.
            // Only reached for TaxMode.Exclusive; the Invoice constructor rejects Inclusive.
            //
            // Step 3 (PerLine):
            //   lineDiscount_i    = Round(lineTotals[i] * DiscountPercent / 100, p)
            //   lineTaxableBase_i = lineTotals[i] - lineDiscount_i
            //   DiscountAmount    = SUM(lineDiscount_i)   [≠ Round(Subtotal * pct) in general]
            var lineBases = new decimal[lineTotals.Length];
            var lineDiscountSum = 0m;
            for (var i = 0; i < lineTotals.Length; i++)
            {
                var ld = Money.Round(lineTotals[i] * input.DiscountPercent / 100m, p);
                lineBases[i] = lineTotals[i] - ld;
                lineDiscountSum += ld;
            }
            discountAmount = lineDiscountSum;
            taxableBase = subtotal - discountAmount;

            // Step 4 (PerLine):
            //   For each rate j: TaxLine_j.Amount = SUM_i Round(lineBases[i] * rate_j / 100, p)
            breakdown = new List<TaxLineResult>(input.TaxRates.Count);
            foreach (var rate in input.TaxRates)
            {
                var amount = 0m;
                foreach (var lineBase in lineBases)
                    amount += Money.Round(lineBase * rate.Percentage / 100m, p);
                breakdown.Add(new TaxLineResult(rate.Name, rate.Percentage, amount));
            }
            taxAmount = breakdown.Sum(t => t.Amount);
        }
        else
        {
            // Steps 3-4 (SubtotalFirst): existing pipeline, unchanged.

            // Step 3: invoice-level discount applied to the net subtotal.
            discountAmount = Money.Round(subtotal * input.DiscountPercent / 100m, p);
            taxableBase = subtotal - discountAmount;

            // Step 4: per-rate tax amounts, additive (never compounded).
            //   taxAmount_j = Round(taxableBase * Percentage_j / 100, p)
            breakdown = input.TaxRates
                .Select(r => new TaxLineResult(
                    r.Name,
                    r.Percentage,
                    Money.Round(taxableBase * r.Percentage / 100m, p)))
                .ToList();

            taxAmount = breakdown.Sum(t => t.Amount);

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
