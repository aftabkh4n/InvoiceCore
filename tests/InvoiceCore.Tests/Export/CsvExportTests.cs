using System.Globalization;
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Export;

public sealed class CsvExportTests
{
    private static InvoiceService Svc() => new();

    private static Invoice SimpleInvoice(string number = "INV-001", string? notes = null)
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = number,
            IssuedDate = new DateOnly(2025, 1, 15),
            DueDate = new DateOnly(2025, 2, 15),
            CurrencyCode = "USD",
            Customer = new CustomerInfo { Name = "Acme Corp", Email = "info@acme.com" },
            LineItems =
            [
                new LineItem { Description = "Widget", Quantity = 1m, UnitPrice = 100m },
            ],
            TaxRates = [new TaxRate { Name = "VAT", Percentage = 20m }],
            Notes = notes,
        };
        return Svc().Create(req);
    }

    // Header row is always present.
    [Fact]
    public void ExportToCsv_includes_header_row()
    {
        var csv = Svc().ExportToCsv([SimpleInvoice()]);
        csv.Should().StartWith("InvoiceNumber,IssuedDate,");
    }

    // Empty collection → header only (no trailing data rows).
    [Fact]
    public void ExportToCsv_empty_collection_is_header_only()
    {
        var csv = Svc().ExportToCsv([]);
        csv.Should().Contain("InvoiceNumber,");
        csv.Lines().Should().HaveCount(2); // header + empty trailing line from last \r\n
    }

    // Dates formatted as yyyy-MM-dd regardless of culture.
    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void ExportToCsv_dates_use_invariant_culture(string cultureName)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var csv = Svc().ExportToCsv([SimpleInvoice()]);
            csv.Should().Contain("2025-01-15");
            csv.Should().Contain("2025-02-15");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    // Decimal values use invariant culture (period as decimal separator, no thousands).
    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void ExportToCsv_decimals_use_invariant_culture(string cultureName)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var csv = Svc().ExportToCsv([SimpleInvoice()]);
            // Subtotal=100, TaxAmount=20, Total=120 — must have period-separated decimals
            (csv.Contains(",100,") || csv.Contains(",100\r\n")).Should().BeTrue();
            csv.Should().NotContain("100,00");   // German format with comma separator
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    // Field containing a comma is quoted (RFC 4180).
    [Fact]
    public void ExportToCsv_quotes_field_containing_comma()
    {
        var inv = new Invoice(
            [new LineItem { Description = "Widget, large", Quantity = 1m, UnitPrice = 10m }],
            [],
            "INV-X",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Joe, Ltd" });

        var csv = Svc().ExportToCsv([inv]);
        csv.Should().Contain("\"Joe, Ltd\"");
    }

    // Field containing a double-quote has the quote doubled (RFC 4180).
    [Fact]
    public void ExportToCsv_doubles_embedded_quotes()
    {
        var inv = new Invoice(
            [new LineItem { Description = "Widget", Quantity = 1m, UnitPrice = 10m }],
            [],
            "INV-X",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" },
            notes: "See \"attachment\"");

        var csv = Svc().ExportToCsv([inv]);
        csv.Should().Contain("\"See \"\"attachment\"\"\"");
    }

    // Field containing a newline is quoted.
    [Fact]
    public void ExportToCsv_quotes_field_containing_newline()
    {
        var inv = new Invoice(
            [new LineItem { Description = "Widget", Quantity = 1m, UnitPrice = 10m }],
            [],
            "INV-X",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" },
            notes: "Line1\nLine2");

        var csv = Svc().ExportToCsv([inv]);
        csv.Should().Contain("\"Line1\nLine2\"");
    }

    // BOM is prepended when requested.
    [Fact]
    public void ExportToCsv_prepends_bom_when_requested()
    {
        var csv = Svc().ExportToCsv([SimpleInvoice()], new CsvExportOptions { IncludeByteOrderMark = true });
        csv[0].Should().Be('﻿');
    }

    // ExportLineItemsToCsv header.
    [Fact]
    public void ExportLineItemsToCsv_includes_header()
    {
        var csv = Svc().ExportLineItemsToCsv([SimpleInvoice()]);
        csv.Should().StartWith("InvoiceNumber,LineNumber,Description,");
    }

    // LineNumber is 1-based.
    [Fact]
    public void ExportLineItemsToCsv_line_numbers_are_one_based()
    {
        var inv = new Invoice(
            [
                new LineItem { Description = "A", Quantity = 1m, UnitPrice = 10m },
                new LineItem { Description = "B", Quantity = 2m, UnitPrice = 5m },
            ],
            [],
            "INV-ML",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        var csv = Svc().ExportLineItemsToCsv([inv]);
        csv.Should().Contain("INV-ML,1,A,");
        csv.Should().Contain("INV-ML,2,B,");
    }

    // Description containing comma is quoted in line-items CSV.
    [Fact]
    public void ExportLineItemsToCsv_quotes_description_with_comma()
    {
        var inv = new Invoice(
            [new LineItem { Description = "widget, large", Quantity = 1m, UnitPrice = 10m }],
            [],
            "INV-X",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        var csv = Svc().ExportLineItemsToCsv([inv]);
        csv.Should().Contain("\"widget, large\"");
    }

    // Culture-swap: tr-TR has dotless-i issue with ToUpper — ensure InvariantCulture used throughout.
    [Fact]
    public void ExportLineItemsToCsv_decimals_invariant_under_tr_TR()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var inv = new Invoice(
                [new LineItem { Description = "X", Quantity = 1.5m, UnitPrice = 10m }],
                [],
                "INV-TR",
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 2, 1),
                new CustomerInfo { Name = "Acme" });

            var csv = Svc().ExportLineItemsToCsv([inv]);
            // Under tr-TR, "1,5" would be wrong; invariant must give "1.5"
            csv.Should().Contain(",1.5,");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}

file static class CsvStringExtensions
{
    public static string[] Lines(this string s) => s.Split("\r\n");
}
