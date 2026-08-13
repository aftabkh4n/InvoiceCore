namespace InvoiceCore;

/// <summary>
/// The outcome of validating a <see cref="CreateInvoiceRequest"/>.
/// Check <see cref="IsValid"/> before consuming <see cref="Errors"/>.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>All validation errors found. Empty when <see cref="IsValid"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary><see langword="true"/> when no validation errors were found.</summary>
    public bool IsValid => Errors.Count == 0;

    internal ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;
}
