namespace InvoiceCore;

/// <summary>The lifecycle state of an invoice.</summary>
public enum InvoiceStatus
{
    /// <summary>The invoice is being prepared and has not been sent to the customer.</summary>
    Draft = 0,

    /// <summary>The invoice has been issued to the customer and awaits payment.</summary>
    Sent = 1,

    /// <summary>Full payment has been received.</summary>
    Paid = 2,

    /// <summary>The invoice was voided before or after sending.</summary>
    Cancelled = 3,

    /// <summary>
    /// Derived display state: the invoice is <see cref="Sent"/> and past its due date.
    /// Never stored or set directly; passing it to <see cref="Invoice.UpdateStatus"/> throws.
    /// </summary>
    Overdue = 4,
}
