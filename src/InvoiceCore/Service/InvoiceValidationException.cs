namespace InvoiceCore;

/// <summary>
/// Thrown by <see cref="IInvoiceService.Create"/> when the
/// <see cref="CreateInvoiceRequest"/> fails validation.
/// </summary>
public sealed class InvoiceValidationException : Exception
{
    /// <summary>The full validation result, including all error codes and messages.</summary>
    public ValidationResult ValidationResult { get; }

    /// <summary>Initialises a new <see cref="InvoiceValidationException"/>.</summary>
    /// <param name="result">The failed validation result. Must not be valid.</param>
    public InvoiceValidationException(ValidationResult result)
        : base("Invoice validation failed.")
    {
        ValidationResult = result;
    }
}
