namespace InvoiceCore;

/// <summary>Currency precision table and the single rounding call site for all money arithmetic.</summary>
internal static class Money
{
    // ISO-4217 zero minor-unit currencies
    private static readonly HashSet<string> s_zeroDecimal = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPY", "KRW", "VND", "CLP", "ISK", "PYG", "RWF", "UGX", "VUV",
        "XAF", "XOF", "XPF", "BIF", "DJF", "GNF", "KMF",
    };

    // ISO-4217 three minor-unit currencies
    private static readonly HashSet<string> s_threeDecimal = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND",
    };

    /// <summary>
    /// Returns the number of minor-unit decimal places for <paramref name="currencyCode"/>.
    /// Unknown or null codes return 2 without throwing. Lookup is case-insensitive.
    /// </summary>
    internal static int GetDecimals(string? currencyCode)
    {
        if (currencyCode is null) return 2;
        if (s_zeroDecimal.Contains(currencyCode)) return 0;
        if (s_threeDecimal.Contains(currencyCode)) return 3;
        return 2;
    }

    /// <summary>
    /// The only rounding call site in InvoiceCore. Uses <see cref="MidpointRounding.AwayFromZero"/>
    /// (half-up), which is what invoice totals and tax authorities expect.
    /// </summary>
    internal static decimal Round(decimal value, int decimals)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
}
