namespace InvoiceCore;

/// <summary>
/// Mutable input DTO for creating an invoice. Pass to <see cref="InvoiceValidator.Validate"/>
/// before constructing an <see cref="Invoice"/>.
/// </summary>
public sealed class CreateInvoiceRequest
{
    /// <summary>Invoice identifier. Defaults to a new <see cref="Guid"/>.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique invoice reference number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Date the invoice is issued.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Payment due date.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>ISO-4217 currency code. Defaults to <c>"USD"</c>.</summary>
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>Whether prices are tax-exclusive or tax-inclusive. Defaults to <see cref="TaxMode.Exclusive"/>.</summary>
    public TaxMode TaxMode { get; set; } = TaxMode.Exclusive;

    /// <summary>Invoice recipient. Required for a valid invoice.</summary>
    public CustomerInfo? Customer { get; set; }

    /// <summary>Line items to bill.</summary>
    public List<LineItem> LineItems { get; set; } = new();

    /// <summary>Tax rates applied at invoice level.</summary>
    public List<TaxRate> TaxRates { get; set; } = new();

    /// <summary>Invoice-level discount percentage (0-100).</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional reporting currency code.</summary>
    public string? BaseCurrencyCode { get; set; }

    /// <summary>Exchange rate: units of <see cref="BaseCurrencyCode"/> per one unit of <see cref="CurrencyCode"/>.</summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Whether to apply tax to the aggregate subtotal or accumulate per-line rounded amounts.
    /// Defaults to <see cref="TaxCalculationMethod.SubtotalFirst"/>.
    /// </summary>
    public TaxCalculationMethod TaxCalculationMethod { get; set; } = TaxCalculationMethod.SubtotalFirst;
}
