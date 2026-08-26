namespace InvoiceCore;

/// <summary>Controls how tax is computed across multiple line items on an invoice.</summary>
public enum TaxCalculationMethod
{
    /// <summary>
    /// Tax is applied once to the aggregate taxable base (sum of rounded line totals, minus the
    /// invoice-level discount). This is the default; all existing behaviour is unchanged.
    /// </summary>
    SubtotalFirst = 0,

    /// <summary>
    /// For each line: apply the invoice-level discount to that line's net total (rounded), then
    /// apply each tax rate to the resulting per-line base (rounded per line per rate), and
    /// accumulate the results into <see cref="Invoice.TaxBreakdown"/> by rate. Use this to match
    /// accounting systems that apply the same per-line procedure.
    /// </summary>
    /// <remarks>
    /// Supported with <see cref="TaxMode.Exclusive"/> only. Combining <see cref="PerLine"/> with
    /// <see cref="TaxMode.Inclusive"/> throws <see cref="NotSupportedException"/> at invoice
    /// construction. See docs/TAX-CONFORMANCE.md (per-line rounding residual analysis) for the
    /// mathematical reason this combination is deferred.
    /// </remarks>
    PerLine = 1,
}
