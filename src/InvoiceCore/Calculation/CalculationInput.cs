namespace InvoiceCore;

/// <summary>A single line for calculation purposes. No validation; use InvoiceValidator upstream.</summary>
internal sealed record LineItemInput(
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent);

/// <summary>A tax rate applied at invoice level.</summary>
internal sealed record TaxRateInput(string Name, decimal Percentage);

/// <summary>
/// Plain data bag passed into <see cref="InvoiceCalculator"/>. Pure input; no validation,
/// no behaviour. Build it from a validated domain object.
/// </summary>
internal sealed record CalculationInput(
    IReadOnlyList<LineItemInput> LineItems,
    IReadOnlyList<TaxRateInput> TaxRates,
    decimal DiscountPercent,
    TaxMode TaxMode,
    string CurrencyCode,
    TaxCalculationMethod TaxCalculationMethod = TaxCalculationMethod.SubtotalFirst);
