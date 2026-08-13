namespace InvoiceCore;

/// <summary>Monetary aggregates for a single currency within an <see cref="InvoiceSummary"/>.</summary>
public sealed class CurrencyTotals
{
    /// <summary>Sum of <see cref="Invoice.Total"/> for all non-cancelled invoices in this currency.</summary>
    public required decimal Total { get; init; }

    /// <summary>Sum of <see cref="Invoice.Total"/> for <see cref="InvoiceStatus.Paid"/> invoices.</summary>
    public required decimal Paid { get; init; }

    /// <summary>
    /// Sum of <see cref="Invoice.Total"/> for <see cref="InvoiceStatus.Sent"/> invoices
    /// (includes overdue ones, which are Sent-and-past-due).
    /// </summary>
    public required decimal Outstanding { get; init; }

    /// <summary>Sum of <see cref="Invoice.Total"/> for invoices where <see cref="Invoice.IsOverdue(TimeProvider?)"/> is <see langword="true"/>.</summary>
    public required decimal Overdue { get; init; }

    /// <summary>Sum of <see cref="Invoice.TaxAmount"/> for all non-cancelled invoices in this currency.</summary>
    public required decimal TaxAmount { get; init; }
}
