using System.Globalization;

namespace InvoiceCore;

/// <summary>A display-ready row in the tax breakdown.</summary>
public sealed class TaxBreakdownRow
{
    /// <summary>Human-readable label, e.g. "VAT 20%".</summary>
    public required string Label { get; init; }

    /// <summary>Formatted tax amount string.</summary>
    public required string Amount { get; init; }
}

/// <summary>A display-ready row for a line item.</summary>
public sealed class LineItemRow
{
    /// <summary>Line item description.</summary>
    public required string Description { get; init; }

    /// <summary>Formatted quantity string.</summary>
    public required string Quantity { get; init; }

    /// <summary>Formatted unit price string.</summary>
    public required string UnitPrice { get; init; }

    /// <summary>Formatted line total string.</summary>
    public required string Total { get; init; }
}

/// <summary>
/// A flattened, display-oriented view of an <see cref="Invoice"/> with formatted
/// money strings. No rendering dependency; intended for consumption by
/// <c>InvoiceCore.Pdf</c> or similar presentation layers.
/// </summary>
public sealed class InvoicePresentation
{
    /// <summary>Invoice reference number.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>Formatted issued date using the short date pattern of the culture passed to <see cref="From"/>.</summary>
    public required string IssuedDate { get; init; }

    /// <summary>Formatted due date.</summary>
    public required string DueDate { get; init; }

    /// <summary>Display string for the stored status.</summary>
    public required string Status { get; init; }

    /// <summary>Customer's display name.</summary>
    public required string CustomerName { get; init; }

    /// <summary>Formatted tax-exclusive subtotal.</summary>
    public required string Subtotal { get; init; }

    /// <summary>Formatted invoice-level discount amount.</summary>
    public required string DiscountAmount { get; init; }

    /// <summary>Formatted total tax amount.</summary>
    public required string TaxAmount { get; init; }

    /// <summary>Formatted grand total.</summary>
    public required string Total { get; init; }

    /// <summary>Per-rate breakdown rows for the totals block.</summary>
    public required IReadOnlyList<TaxBreakdownRow> TaxBreakdown { get; init; }

    /// <summary>Line item rows for the invoice body.</summary>
    public required IReadOnlyList<LineItemRow> LineItems { get; init; }

    /// <summary>
    /// Creates an <see cref="InvoicePresentation"/> from <paramref name="invoice"/>,
    /// formatting money and dates for <paramref name="culture"/> (defaults to
    /// <see cref="CultureInfo.CurrentCulture"/> when <see langword="null"/>).
    /// </summary>
    /// <param name="invoice">The invoice to present.</param>
    /// <param name="culture">Culture used for number and date formatting.</param>
    public static InvoicePresentation From(Invoice invoice, CultureInfo? culture = null)
    {
        var fmt = culture ?? CultureInfo.CurrentCulture;
        return new InvoicePresentation
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssuedDate = invoice.IssuedDate.ToString("d", fmt),
            DueDate = invoice.DueDate.ToString("d", fmt),
            Status = invoice.Status.ToString(),
            CustomerName = invoice.Customer.Name,
            Subtotal = invoice.Subtotal.ToString("C", fmt),
            DiscountAmount = invoice.DiscountAmount.ToString("C", fmt),
            TaxAmount = invoice.TaxAmount.ToString("C", fmt),
            Total = invoice.Total.ToString("C", fmt),
            TaxBreakdown = invoice.TaxBreakdown
                .Select(t => new TaxBreakdownRow
                {
                    Label = $"{t.Name} {t.Percentage:G}%",
                    Amount = t.Amount.ToString("C", fmt),
                })
                .ToArray(),
            LineItems = invoice.LineItems
                .Select(l => new LineItemRow
                {
                    Description = l.Description,
                    Quantity = l.Quantity.ToString("G", fmt),
                    UnitPrice = l.UnitPrice.ToString("C", fmt),
                    Total = l.Total.ToString("C", fmt),
                })
                .ToArray(),
        };
    }
}
