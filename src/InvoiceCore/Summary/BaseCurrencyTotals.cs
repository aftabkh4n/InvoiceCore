namespace InvoiceCore;

/// <summary>
/// Cross-currency rollup in the common reporting currency. Only present when
/// every non-cancelled invoice in the summary has both
/// <see cref="Invoice.BaseCurrencyCode"/> and <see cref="Invoice.ExchangeRate"/> set
/// to the same base currency.
/// </summary>
public sealed class BaseCurrencyTotals
{
    /// <summary>The common reporting currency code (e.g. "USD").</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>Sum of <c>Invoice.Total × ExchangeRate</c> for all non-cancelled invoices.</summary>
    public required decimal Total { get; init; }

    /// <summary>Sum of converted totals for <see cref="InvoiceStatus.Paid"/> invoices.</summary>
    public required decimal Paid { get; init; }

    /// <summary>Sum of converted totals for <see cref="InvoiceStatus.Sent"/> invoices.</summary>
    public required decimal Outstanding { get; init; }

    /// <summary>Sum of converted totals for overdue invoices.</summary>
    public required decimal Overdue { get; init; }

    /// <summary>Sum of <c>Invoice.TaxAmount × ExchangeRate</c> for all non-cancelled invoices.</summary>
    public required decimal TaxAmount { get; init; }
}
