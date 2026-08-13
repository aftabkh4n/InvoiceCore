namespace InvoiceCore;

/// <summary>Whether line-item prices already include tax or have it added on top.</summary>
internal enum TaxMode
{
    /// <summary>Tax is added to the net subtotal.</summary>
    Exclusive = 0,

    /// <summary>Tax is embedded in the line prices; <c>Subtotal</c> is the back-calculated net.</summary>
    Inclusive = 1,
}
