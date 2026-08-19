using System.Diagnostics.CodeAnalysis;

namespace InvoiceCore;

/// <summary>Controls how monetary amounts are serialised in JSON export.</summary>
public enum MoneyFormat
{
    /// <summary>
    /// Monetary amounts are written as quoted decimal strings formatted to the invoice
    /// currency's minor-unit precision using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
    /// Trailing zeros are preserved: USD $10.50 serialises as <c>"10.50"</c>, JPY 1000
    /// as <c>"1000"</c>, KWD 100.010 as <c>"100.010"</c>.
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name",
        Justification = "'String' unambiguously describes the serialisation format for a money-format enum.")]
    String,

    /// <summary>
    /// Monetary amounts are written as raw JSON numbers. Trailing zeros are stripped by
    /// the serialiser: $10.50 may appear as <c>10.5</c>. Non-.NET consumers that parse
    /// these as IEEE 754 doubles lose exact decimal semantics.
    /// </summary>
    Number,
}
