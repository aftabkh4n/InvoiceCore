using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Export;

public sealed class JsonExportTests
{
    private static InvoiceService Svc() => new();

    private static Invoice SimpleInvoice()
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-001",
            IssuedDate = new DateOnly(2025, 1, 15),
            DueDate = new DateOnly(2025, 2, 15),
            CurrencyCode = "USD",
            Customer = new CustomerInfo { Name = "Acme" },
            LineItems =
            [
                new LineItem { Description = "Widget", Quantity = 2m, UnitPrice = 50m },
            ],
            TaxRates = [new TaxRate { Name = "VAT", Percentage = 20m }],
        };
        return Svc().Create(req);
    }

    // JSON output is valid JSON.
    [Fact]
    public void ExportToJson_produces_valid_json()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    // Keys are camelCase.
    [Fact]
    public void ExportToJson_uses_camel_case_keys()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        json.Should().Contain("\"invoiceNumber\"");
        json.Should().NotContain("\"InvoiceNumber\"");
    }

    // IssuedDate formatted as yyyy-MM-dd (ISO 8601).
    [Fact]
    public void ExportToJson_dates_are_iso8601()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        json.Should().Contain("\"2025-01-15\"");
        json.Should().Contain("\"2025-02-15\"");
    }

    // Status serialised as string, not integer.
    [Fact]
    public void ExportToJson_status_is_string()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        json.Should().Contain("\"Draft\"");
        json.Should().NotMatchRegex("\"status\":\\s*0");
    }

    // TaxMode serialised as string.
    [Fact]
    public void ExportToJson_taxMode_is_string()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        json.Should().Contain("\"Exclusive\"");
    }

    // Computed totals are in the snapshot.
    [Fact]
    public void ExportToJson_includes_computed_totals()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("subtotal").GetDecimal().Should().Be(100m);
        root.GetProperty("taxAmount").GetDecimal().Should().Be(20m);
        root.GetProperty("total").GetDecimal().Should().Be(120m);
    }

    // Not indented by default.
    [Fact]
    public void ExportToJson_not_indented_by_default()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        json.Should().NotContain("\n");
    }

    // Indented when option set.
    [Fact]
    public void ExportToJson_indented_when_option_set()
    {
        var json = Svc().ExportToJson(SimpleInvoice(), new JsonExportOptions { Indented = true });
        json.Should().Contain("\n");
    }

    // Array overload returns a JSON array.
    [Fact]
    public void ExportToJson_array_returns_json_array()
    {
        var inv = SimpleInvoice();
        var json = Svc().ExportToJson([inv, inv]);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }

    // Empty array overload → empty JSON array.
    [Fact]
    public void ExportToJson_empty_array_is_empty_json_array()
    {
        var json = Svc().ExportToJson(Array.Empty<Invoice>());
        json.Should().Be("[]");
    }

    // Source-gen context resolves InvoiceDto type info (smoke: no reflection path used).
    [Fact]
    public void Source_gen_context_resolves_InvoiceDto_type_info()
    {
        var typeInfo = InvoiceJsonContext.Default.GetTypeInfo(typeof(InvoiceDto));
        typeInfo.Should().NotBeNull();
    }

    // taxBreakdown array is present and populated.
    [Fact]
    public void ExportToJson_taxBreakdown_present()
    {
        var json = Svc().ExportToJson(SimpleInvoice());
        using var doc = JsonDocument.Parse(json);
        var breakdown = doc.RootElement.GetProperty("taxBreakdown");
        breakdown.GetArrayLength().Should().Be(1);
        breakdown[0].GetProperty("name").GetString().Should().Be("VAT");
        breakdown[0].GetProperty("amount").GetDecimal().Should().Be(20m);
    }
}
