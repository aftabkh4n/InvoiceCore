namespace InvoiceCore;

/// <summary>One entry in the per-rate tax breakdown.</summary>
internal sealed record TaxLineResult(string Name, decimal Percentage, decimal Amount);

/// <summary>
/// Immutable snapshot of a completed invoice calculation. All decimal values are already
/// rounded to the currency's minor-unit precision — callers never need to round again.
/// <list type="bullet">
/// <item><term>LineTotals</term><description>Per-line totals, same order as input LineItems.</description></item>
/// <item><term>Subtotal</term><description>Tax-exclusive net amount — always net in both tax modes.</description></item>
/// <item><term>DiscountAmount</term><description>Invoice-level discount applied to Subtotal.</description></item>
/// <item><term>TaxAmount</term><description>Sum of all per-rate tax amounts after reconciliation.</description></item>
/// <item><term>Total</term><description>Amount the customer owes: taxable base plus tax.</description></item>
/// <item><term>TaxBreakdown</term><description>Per-rate breakdown for PDF rendering and tax filing.</description></item>
/// </list>
/// </summary>
internal sealed record CalculationResult(
    IReadOnlyList<decimal> LineTotals,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    IReadOnlyList<TaxLineResult> TaxBreakdown);
