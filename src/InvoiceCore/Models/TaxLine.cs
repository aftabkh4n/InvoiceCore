namespace InvoiceCore;

/// <summary>The calculated tax amount for one rate in the invoice breakdown.</summary>
public sealed class TaxLine
{
    /// <summary>Rate name, copied from <see cref="TaxRate.Name"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Rate percentage, copied from <see cref="TaxRate.Percentage"/>.</summary>
    public required decimal Percentage { get; init; }

    /// <summary>Computed tax amount for this rate, rounded to currency precision.</summary>
    public required decimal Amount { get; init; }
}
