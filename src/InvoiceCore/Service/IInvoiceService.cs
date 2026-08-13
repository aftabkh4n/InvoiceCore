namespace InvoiceCore;

/// <summary>
/// Core service contract for creating, transitioning, exporting, and summarising invoices.
/// </summary>
public interface IInvoiceService
{
    /// <summary>
    /// Validates <paramref name="request"/> and, if valid, constructs and returns an <see cref="Invoice"/>.
    /// </summary>
    /// <param name="request">The creation request.</param>
    /// <returns>A new invoice in <see cref="InvoiceStatus.Draft"/> state.</returns>
    /// <exception cref="InvoiceValidationException">Thrown when validation fails.</exception>
    Invoice Create(CreateInvoiceRequest request);

    /// <summary>
    /// Applies a status transition to <paramref name="invoice"/> and returns the same instance.
    /// </summary>
    /// <param name="invoice">The invoice to transition.</param>
    /// <param name="status">The target status.</param>
    /// <returns>The same <paramref name="invoice"/> instance after mutation.</returns>
    /// <exception cref="InvalidStatusTransitionException">The transition is not permitted.</exception>
    Invoice UpdateStatus(Invoice invoice, InvoiceStatus status);

    /// <summary>
    /// Validates <paramref name="request"/> and returns the result without throwing.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    ValidationResult Validate(CreateInvoiceRequest request);

    /// <summary>Serialises <paramref name="invoice"/> to a JSON snapshot string.</summary>
    /// <param name="invoice">The invoice to export.</param>
    /// <param name="options">Optional serialisation settings.</param>
    string ExportToJson(Invoice invoice, JsonExportOptions? options = null);

    /// <summary>Serialises <paramref name="invoices"/> to a JSON array snapshot string.</summary>
    /// <param name="invoices">The invoices to export.</param>
    /// <param name="options">Optional serialisation settings.</param>
    string ExportToJson(IEnumerable<Invoice> invoices, JsonExportOptions? options = null);

    /// <summary>
    /// Exports <paramref name="invoices"/> as RFC 4180 CSV with one row per invoice.
    /// All decimals and dates use <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="invoices">The invoices to export.</param>
    /// <param name="options">Optional CSV settings (e.g. BOM).</param>
    string ExportToCsv(IEnumerable<Invoice> invoices, CsvExportOptions? options = null);

    /// <summary>
    /// Exports one row per line item across all <paramref name="invoices"/>, with
    /// <c>InvoiceNumber</c> as the join key. RFC 4180, InvariantCulture.
    /// </summary>
    /// <param name="invoices">The invoices whose line items to export.</param>
    /// <param name="options">Optional CSV settings.</param>
    string ExportLineItemsToCsv(IEnumerable<Invoice> invoices, CsvExportOptions? options = null);

    /// <summary>Returns invoices where <see cref="Invoice.IsOverdue(TimeProvider?)"/> is <see langword="true"/>.</summary>
    /// <param name="invoices">The collection to filter.</param>
    IEnumerable<Invoice> GetOverdue(IEnumerable<Invoice> invoices);

    /// <summary>
    /// Computes aggregate totals grouped by currency. Cancelled invoices are excluded
    /// from monetary totals but counted in <see cref="InvoiceSummary.CountByStatus"/>.
    /// </summary>
    /// <param name="invoices">The collection to summarise.</param>
    InvoiceSummary GetSummary(IEnumerable<Invoice> invoices);
}
