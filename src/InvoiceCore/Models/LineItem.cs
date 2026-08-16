namespace InvoiceCore;

/// <summary>A single billed line on an invoice with a computed rounded total.</summary>
public sealed class LineItem
{
    /// <summary>Description of the product or service.</summary>
    public required string Description { get; init; }

    /// <summary>Number of units billed. Must be greater than zero.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Price per unit before discount.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Optional per-line discount as a percentage (0-100).</summary>
    public decimal DiscountPercent { get; init; }

    /// <summary>
    /// Rounded line total after applying <see cref="DiscountPercent"/>, computed by
    /// <see cref="Invoice"/> during construction. Callers do not set this.
    /// </summary>
    public decimal Total { get; internal init; }
}
