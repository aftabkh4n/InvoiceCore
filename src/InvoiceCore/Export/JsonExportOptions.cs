namespace InvoiceCore;

/// <summary>Controls JSON serialisation behaviour for invoice export.</summary>
public sealed class JsonExportOptions
{
    /// <summary>When <see langword="true"/>, the JSON output is indented for readability. Defaults to <see langword="false"/>.</summary>
    public bool Indented { get; init; }

    /// <summary>
    /// How monetary amounts are serialised. Defaults to <see cref="MoneyFormat.String"/>,
    /// which preserves exact decimal semantics and trailing zeros for all consumers.
    /// Use <see cref="MoneyFormat.Number"/> only when the receiving system is known to
    /// handle <see cref="decimal"/> correctly.
    /// </summary>
    public MoneyFormat MoneyFormat { get; init; } = MoneyFormat.String;

    /// <summary>
    /// When <see langword="true"/>, null-valued optional fields (<c>notes</c>,
    /// <c>baseCurrencyCode</c>, <c>exchangeRate</c>, customer sub-fields) are written
    /// explicitly as JSON <c>null</c>. Defaults to <see langword="false"/> (nulls omitted).
    /// </summary>
    public bool IncludeNulls { get; init; }
}
