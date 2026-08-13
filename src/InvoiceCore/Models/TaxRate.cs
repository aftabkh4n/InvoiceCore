namespace InvoiceCore;

/// <summary>A named percentage rate applied at the invoice level.</summary>
public sealed class TaxRate
{
    /// <summary>Human-readable label, e.g. "VAT", "GST", or "Sales Tax".</summary>
    public required string Name { get; init; }

    /// <summary>Rate as a percentage: 20m means 20%.</summary>
    public required decimal Percentage { get; init; }
}
