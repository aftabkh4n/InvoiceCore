namespace InvoiceCore;

/// <summary>
/// Aggregate view of a collection of invoices. Monetary totals are grouped
/// per-currency to avoid meaningless cross-currency sums.
/// Cancelled invoices are excluded from monetary totals but counted in
/// <see cref="CountByStatus"/>.
/// </summary>
public sealed class InvoiceSummary
{
    /// <summary>Total number of invoices in the collection (including cancelled).</summary>
    public required int Count { get; init; }

    /// <summary>Count of invoices per stored <see cref="InvoiceStatus"/>.</summary>
    public required IReadOnlyDictionary<InvoiceStatus, int> CountByStatus { get; init; }

    /// <summary>
    /// Monetary aggregates keyed by <see cref="Invoice.CurrencyCode"/>.
    /// Currencies with no non-cancelled invoices are omitted.
    /// </summary>
    public required IReadOnlyDictionary<string, CurrencyTotals> ByCurrency { get; init; }

    /// <summary>
    /// Cross-currency rollup in the common reporting currency.
    /// <see langword="null"/> unless every non-cancelled invoice has a
    /// <see cref="Invoice.BaseCurrencyCode"/> and <see cref="Invoice.ExchangeRate"/>
    /// pointing to the same base currency.
    /// </summary>
    public BaseCurrencyTotals? Rollup { get; init; }
}
