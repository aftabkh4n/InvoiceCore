namespace InvoiceCore;

/// <summary>A single validation failure with a machine-readable code and a human-readable message.</summary>
/// <param name="PropertyPath">Dot-notation path to the offending property, e.g. <c>"LineItems[0].Quantity"</c>.</param>
/// <param name="Message">Human-readable description of the failure.</param>
/// <param name="Code">Machine-readable error code, e.g. <c>"LINE_QUANTITY_INVALID"</c>.</param>
public sealed record ValidationError(string PropertyPath, string Message, string Code);
