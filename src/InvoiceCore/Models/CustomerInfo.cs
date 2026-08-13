namespace InvoiceCore;

/// <summary>Contact and tax details for the invoice recipient.</summary>
public sealed class CustomerInfo
{
    /// <summary>Legal or display name of the customer.</summary>
    public required string Name { get; init; }

    /// <summary>Optional contact email address.</summary>
    public string? Email { get; init; }

    /// <summary>Optional mailing or billing address.</summary>
    public string? Address { get; init; }

    /// <summary>Optional VAT or tax registration number.</summary>
    public string? TaxNumber { get; init; }
}
