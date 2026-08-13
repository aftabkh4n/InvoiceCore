using System.Text.Json.Serialization;

namespace InvoiceCore;

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
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InvoiceDto))]
[JsonSerializable(typeof(InvoiceDto[]))]
internal sealed partial class InvoiceJsonContextIndented : JsonSerializerContext
{
}
