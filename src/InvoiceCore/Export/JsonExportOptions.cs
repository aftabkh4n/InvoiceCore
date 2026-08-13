namespace InvoiceCore;

/// <summary>Controls JSON serialisation behaviour for invoice export.</summary>
public sealed class JsonExportOptions
{
    /// <summary>When <see langword="true"/>, the JSON output is indented for readability. Defaults to <see langword="false"/>.</summary>
    public bool Indented { get; init; }
}
