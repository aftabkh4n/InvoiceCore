using System.Text.Json.Serialization;

namespace InvoiceCore;

// ---------------------------------------------------------------------------
// Number-mode contexts (monetary fields are JSON numbers)
// ---------------------------------------------------------------------------

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InvoiceDto))]
[JsonSerializable(typeof(InvoiceDto[]))]
internal sealed partial class InvoiceJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InvoiceDto))]
[JsonSerializable(typeof(InvoiceDto[]))]
internal sealed partial class InvoiceJsonContextNoNulls : JsonSerializerContext
{
}

// ---------------------------------------------------------------------------
// String-mode contexts (monetary fields are quoted decimal strings)
// ---------------------------------------------------------------------------

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InvoiceStringDto))]
[JsonSerializable(typeof(InvoiceStringDto[]))]
internal sealed partial class InvoiceStringJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InvoiceStringDto))]
[JsonSerializable(typeof(InvoiceStringDto[]))]
internal sealed partial class InvoiceStringJsonContextWithNulls : JsonSerializerContext
{
}
