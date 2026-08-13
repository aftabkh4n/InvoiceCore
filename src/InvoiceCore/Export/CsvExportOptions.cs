namespace InvoiceCore;

/// <summary>Controls CSV serialisation behaviour for invoice export.</summary>
public sealed class CsvExportOptions
{
    /// <summary>
    /// When <see langword="true"/>, prepends the UTF-8 byte-order mark (<c>﻿</c>) so that
    /// Excel auto-detects the encoding. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IncludeByteOrderMark { get; init; }
}
