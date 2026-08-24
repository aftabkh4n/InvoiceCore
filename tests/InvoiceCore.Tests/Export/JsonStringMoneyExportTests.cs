using System.Globalization;
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Export;

public sealed class JsonStringMoneyExportTests
{
    private static InvoiceService Svc() => new();

    private static Invoice GoldenInvoice() => new(
        [new LineItem { Description = "Consulting", Quantity = 2m, UnitPrice = 75m }],
        [new TaxRate { Name = "VAT", Percentage = 20m }],
        "INV-GOLDEN",
        new DateOnly(2025, 1, 1),
        new DateOnly(2025, 2, 1),
        new CustomerInfo { Name = "Acme Corp", Email = "billing@acme.com" },
        id: new Guid("00000000-0000-0000-0000-000000000001"));

    // --- Golden snapshots for all four mode combinations ---

    [Fact]
    public void ExportToJson_StringNoNulls_golden_snapshot()
    {
        const string Expected =
            """{"id":"00000000-0000-0000-0000-000000000001","invoiceNumber":"INV-GOLDEN","issuedDate":"2025-01-01","dueDate":"2025-02-01","status":"Draft","currencyCode":"USD","taxMode":"Exclusive","taxCalculationMethod":"SubtotalFirst","customer":{"name":"Acme Corp","email":"billing@acme.com"},"lineItems":[{"description":"Consulting","quantity":2,"unitPrice":"75.00","discountPercent":0,"total":"150.00"}],"taxRates":[{"name":"VAT","percentage":20}],"discountPercent":0,"subtotal":"150.00","discountAmount":"0.00","taxAmount":"30.00","total":"180.00","taxBreakdown":[{"name":"VAT","percentage":20,"amount":"30.00"}]}""";

        Svc().ExportToJson(GoldenInvoice()).Should().Be(Expected);
    }

    [Fact]
    public void ExportToJson_StringWithNulls_golden_snapshot()
    {
        const string Expected =
            """{"id":"00000000-0000-0000-0000-000000000001","invoiceNumber":"INV-GOLDEN","issuedDate":"2025-01-01","dueDate":"2025-02-01","status":"Draft","currencyCode":"USD","taxMode":"Exclusive","taxCalculationMethod":"SubtotalFirst","customer":{"name":"Acme Corp","email":"billing@acme.com","address":null,"taxNumber":null},"lineItems":[{"description":"Consulting","quantity":2,"unitPrice":"75.00","discountPercent":0,"total":"150.00"}],"taxRates":[{"name":"VAT","percentage":20}],"discountPercent":0,"notes":null,"baseCurrencyCode":null,"exchangeRate":null,"subtotal":"150.00","discountAmount":"0.00","taxAmount":"30.00","total":"180.00","taxBreakdown":[{"name":"VAT","percentage":20,"amount":"30.00"}]}""";

        Svc().ExportToJson(GoldenInvoice(),
            new JsonExportOptions { MoneyFormat = MoneyFormat.String, IncludeNulls = true })
            .Should().Be(Expected);
    }

    [Fact]
    public void ExportToJson_NumberNoNulls_golden_snapshot()
    {
        const string Expected =
            """{"id":"00000000-0000-0000-0000-000000000001","invoiceNumber":"INV-GOLDEN","issuedDate":"2025-01-01","dueDate":"2025-02-01","status":"Draft","currencyCode":"USD","taxMode":"Exclusive","taxCalculationMethod":"SubtotalFirst","customer":{"name":"Acme Corp","email":"billing@acme.com"},"lineItems":[{"description":"Consulting","quantity":2,"unitPrice":75,"discountPercent":0,"total":150}],"taxRates":[{"name":"VAT","percentage":20}],"discountPercent":0,"subtotal":150,"discountAmount":0,"taxAmount":30,"total":180,"taxBreakdown":[{"name":"VAT","percentage":20,"amount":30}]}""";

        Svc().ExportToJson(GoldenInvoice(),
            new JsonExportOptions { MoneyFormat = MoneyFormat.Number })
            .Should().Be(Expected);
    }

    [Fact]
    public void ExportToJson_NumberWithNulls_golden_snapshot()
    {
        const string Expected =
            """{"id":"00000000-0000-0000-0000-000000000001","invoiceNumber":"INV-GOLDEN","issuedDate":"2025-01-01","dueDate":"2025-02-01","status":"Draft","currencyCode":"USD","taxMode":"Exclusive","taxCalculationMethod":"SubtotalFirst","customer":{"name":"Acme Corp","email":"billing@acme.com","address":null,"taxNumber":null},"lineItems":[{"description":"Consulting","quantity":2,"unitPrice":75,"discountPercent":0,"total":150}],"taxRates":[{"name":"VAT","percentage":20}],"discountPercent":0,"notes":null,"baseCurrencyCode":null,"exchangeRate":null,"subtotal":150,"discountAmount":0,"taxAmount":30,"total":180,"taxBreakdown":[{"name":"VAT","percentage":20,"amount":30}]}""";

        Svc().ExportToJson(GoldenInvoice(),
            new JsonExportOptions { MoneyFormat = MoneyFormat.Number, IncludeNulls = true })
            .Should().Be(Expected);
    }

    // --- Currency precision ---

    // JPY has 0 decimal places; monetary strings must be integers, never "1000.00".
    [Fact]
    public void ExportToJson_String_JPY_zero_decimal_places()
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-JPY",
            IssuedDate = new DateOnly(2025, 1, 1),
            DueDate = new DateOnly(2025, 2, 1),
            CurrencyCode = "JPY",
            Customer = new CustomerInfo { Name = "Test" },
            LineItems = [new LineItem { Description = "Item", Quantity = 1m, UnitPrice = 1000m }],
            TaxRates = [],
        };
        var inv = Svc().Create(req);
        var json = Svc().ExportToJson(inv);

        json.Should().Contain("\"unitPrice\":\"1000\"");
        json.Should().Contain("\"total\":\"1000\"");
        json.Should().Contain("\"subtotal\":\"1000\"");
        json.Should().NotContain("\"1000.00\"");
    }

    // KWD has 3 decimal places; trailing zeros must be preserved.
    [Fact]
    public void ExportToJson_String_KWD_three_decimal_places()
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-KWD",
            IssuedDate = new DateOnly(2025, 1, 1),
            DueDate = new DateOnly(2025, 2, 1),
            CurrencyCode = "KWD",
            Customer = new CustomerInfo { Name = "Test" },
            LineItems = [new LineItem { Description = "Item", Quantity = 1m, UnitPrice = 100.010m }],
            TaxRates = [],
        };
        var inv = Svc().Create(req);
        var json = Svc().ExportToJson(inv);

        json.Should().Contain("\"unitPrice\":\"100.010\"");
        json.Should().Contain("\"total\":\"100.010\"");
    }

    // USD $10.50 must appear as "10.50", not "10.5" (trailing zero preserved).
    [Fact]
    public void ExportToJson_String_trailing_zeros_preserved()
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-TZ",
            IssuedDate = new DateOnly(2025, 1, 1),
            DueDate = new DateOnly(2025, 2, 1),
            CurrencyCode = "USD",
            Customer = new CustomerInfo { Name = "Test" },
            LineItems = [new LineItem { Description = "Item", Quantity = 1m, UnitPrice = 10.50m }],
            TaxRates = [],
        };
        var inv = Svc().Create(req);
        var json = Svc().ExportToJson(inv);

        json.Should().Contain("\"10.50\"");
        json.Should().NotContain("\"10.5\"");
    }

    // --- Mutation guards ---

    // M17: String mode must use InvariantCulture, not CurrentCulture.
    // Under cultures with comma decimal separators the output must still use '.'.
    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void ExportToJson_String_uses_invariant_culture(string cultureName)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            var req = new CreateInvoiceRequest
            {
                InvoiceNumber = "INV-CULTURE",
                IssuedDate = new DateOnly(2025, 1, 1),
                DueDate = new DateOnly(2025, 2, 1),
                CurrencyCode = "USD",
                Customer = new CustomerInfo { Name = "Test" },
                LineItems = [new LineItem { Description = "Item", Quantity = 1m, UnitPrice = 10.50m }],
                TaxRates = [],
            };
            var inv = Svc().Create(req);
            var json = Svc().ExportToJson(inv);

            json.Should().Contain("\"10.50\"");
            json.Should().NotContain("\"10,50\"");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    // M18: Formatting uses currency precision, not a strip-trailing-zeros format like "0.##".
    // For USD (2dp): discountAmount=0 → "0.00", not "0" or "0.0".
    [Fact]
    public void ExportToJson_String_uses_currency_precision_not_generic_format()
    {
        var req = new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-PREC",
            IssuedDate = new DateOnly(2025, 1, 1),
            DueDate = new DateOnly(2025, 2, 1),
            CurrencyCode = "USD",
            Customer = new CustomerInfo { Name = "Test" },
            LineItems = [new LineItem { Description = "Item", Quantity = 1m, UnitPrice = 100m }],
            TaxRates = [],
        };
        var inv = Svc().Create(req);
        var json = Svc().ExportToJson(inv);

        json.Should().Contain("\"discountAmount\":\"0.00\"");
        json.Should().Contain("\"subtotal\":\"100.00\"");
        json.Should().NotContain("\"discountAmount\":\"0\"");
    }

    // M19: IncludeNulls default is false — null optional fields are absent without explicit option.
    [Fact]
    public void ExportToJson_default_excludes_null_fields()
    {
        var json = Svc().ExportToJson(GoldenInvoice());

        json.Should().NotContain("\"notes\"");
        json.Should().NotContain("\"baseCurrencyCode\"");
        json.Should().NotContain("\"exchangeRate\"");
        json.Should().NotContain("\"address\"");
        json.Should().NotContain("\"taxNumber\"");
    }
}
