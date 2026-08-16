using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Models;

/// <summary>One test per SPEC §7 error code, plus a multi-error test.</summary>
public sealed class ValidationTests
{
    private static CreateInvoiceRequest Valid() => new()
    {
        InvoiceNumber = "INV-001",
        IssuedDate = new DateOnly(2025, 1, 1),
        DueDate = new DateOnly(2025, 2, 1),
        CurrencyCode = "USD",
        Customer = new CustomerInfo { Name = "Acme" },
        LineItems =
        [
            new LineItem { Description = "Consulting", Quantity = 1m, UnitPrice = 100m },
        ],
        TaxRates = [],
    };

    private static ValidationResult Validate(Action<CreateInvoiceRequest> mutate)
    {
        var req = Valid();
        mutate(req);
        return InvoiceValidator.Validate(req);
    }

    private static void HasCode(ValidationResult result, string code)
        => result.Errors.Should().ContainSingle(e => e.Code == code);

    // INVOICE_NUMBER_REQUIRED
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void INVOICE_NUMBER_REQUIRED(string? value)
    {
        var result = Validate(r => r.InvoiceNumber = value!);
        HasCode(result, "INVOICE_NUMBER_REQUIRED");
    }

    // CUSTOMER_REQUIRED: null customer
    [Fact]
    public void CUSTOMER_REQUIRED_null_customer()
    {
        var result = Validate(r => r.Customer = null);
        HasCode(result, "CUSTOMER_REQUIRED");
    }

    // CUSTOMER_REQUIRED: empty name
    [Fact]
    public void CUSTOMER_REQUIRED_empty_name()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "  " });
        HasCode(result, "CUSTOMER_REQUIRED");
    }

    // DUE_BEFORE_ISSUE
    [Fact]
    public void DUE_BEFORE_ISSUE()
    {
        var result = Validate(r =>
        {
            r.IssuedDate = new DateOnly(2025, 3, 1);
            r.DueDate = new DateOnly(2025, 2, 1);
        });
        HasCode(result, "DUE_BEFORE_ISSUE");
    }

    // DUE_BEFORE_ISSUE: same date is valid
    [Fact]
    public void Same_issued_and_due_date_is_valid()
    {
        var result = Validate(r => r.DueDate = r.IssuedDate);
        result.Errors.Should().NotContain(e => e.Code == "DUE_BEFORE_ISSUE");
    }

    // CURRENCY_INVALID: lowercase
    [Fact]
    public void CURRENCY_INVALID_lowercase()
    {
        var result = Validate(r => r.CurrencyCode = "usd");
        HasCode(result, "CURRENCY_INVALID");
    }

    // CURRENCY_INVALID: wrong length
    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void CURRENCY_INVALID_wrong_length(string code)
    {
        var result = Validate(r => r.CurrencyCode = code);
        HasCode(result, "CURRENCY_INVALID");
    }

    // LINE_ITEMS_REQUIRED
    [Fact]
    public void LINE_ITEMS_REQUIRED()
    {
        var result = Validate(r => r.LineItems.Clear());
        HasCode(result, "LINE_ITEMS_REQUIRED");
    }

    // LINE_DESCRIPTION_REQUIRED
    [Fact]
    public void LINE_DESCRIPTION_REQUIRED()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "", Quantity = 1m, UnitPrice = 10m });
        HasCode(result, "LINE_DESCRIPTION_REQUIRED");
    }

    // LINE_QUANTITY_INVALID: zero
    [Fact]
    public void LINE_QUANTITY_INVALID_zero()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = 0m, UnitPrice = 10m });
        HasCode(result, "LINE_QUANTITY_INVALID");
    }

    // LINE_QUANTITY_INVALID: negative
    [Fact]
    public void LINE_QUANTITY_INVALID_negative()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = -1m, UnitPrice = 10m });
        HasCode(result, "LINE_QUANTITY_INVALID");
    }

    // LINE_UNIT_PRICE_INVALID: negative (zero is allowed)
    [Fact]
    public void LINE_UNIT_PRICE_INVALID_negative()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = 1m, UnitPrice = -0.01m });
        HasCode(result, "LINE_UNIT_PRICE_INVALID");
    }

    // LINE_UNIT_PRICE_INVALID: zero is valid
    [Fact]
    public void LINE_UNIT_PRICE_zero_is_valid()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = 1m, UnitPrice = 0m });
        result.Errors.Should().NotContain(e => e.Code == "LINE_UNIT_PRICE_INVALID");
    }

    // LINE_DISCOUNT_RANGE: below 0
    [Fact]
    public void LINE_DISCOUNT_RANGE_below_zero()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m, DiscountPercent = -1m });
        HasCode(result, "LINE_DISCOUNT_RANGE");
    }

    // LINE_DISCOUNT_RANGE: above 100
    [Fact]
    public void LINE_DISCOUNT_RANGE_above_hundred()
    {
        var result = Validate(r => r.LineItems[0] = new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m, DiscountPercent = 101m });
        HasCode(result, "LINE_DISCOUNT_RANGE");
    }

    // INVOICE_DISCOUNT_RANGE
    [Fact]
    public void INVOICE_DISCOUNT_RANGE()
    {
        var result = Validate(r => r.DiscountPercent = 101m);
        HasCode(result, "INVOICE_DISCOUNT_RANGE");
    }

    // TAX_NAME_REQUIRED
    [Fact]
    public void TAX_NAME_REQUIRED()
    {
        var result = Validate(r => r.TaxRates.Add(new TaxRate { Name = "", Percentage = 10m }));
        HasCode(result, "TAX_NAME_REQUIRED");
    }

    // TAX_PERCENTAGE_RANGE: negative
    [Fact]
    public void TAX_PERCENTAGE_RANGE_negative()
    {
        var result = Validate(r => r.TaxRates.Add(new TaxRate { Name = "VAT", Percentage = -1m }));
        HasCode(result, "TAX_PERCENTAGE_RANGE");
    }

    // TAX_PERCENTAGE_RANGE: above 100
    [Fact]
    public void TAX_PERCENTAGE_RANGE_above_hundred()
    {
        var result = Validate(r => r.TaxRates.Add(new TaxRate { Name = "VAT", Percentage = 101m }));
        HasCode(result, "TAX_PERCENTAGE_RANGE");
    }

    // TAX_DUPLICATE_NAME: case-insensitive
    [Fact]
    public void TAX_DUPLICATE_NAME_case_insensitive()
    {
        var result = Validate(r =>
        {
            r.TaxRates.Add(new TaxRate { Name = "VAT", Percentage = 10m });
            r.TaxRates.Add(new TaxRate { Name = "vat", Percentage = 5m });
        });
        HasCode(result, "TAX_DUPLICATE_NAME");
    }

    // EXCHANGE_RATE_INVALID: rate set but no BaseCurrencyCode
    [Fact]
    public void EXCHANGE_RATE_INVALID_missing_base()
    {
        var result = Validate(r => r.ExchangeRate = 1.27m);
        HasCode(result, "EXCHANGE_RATE_INVALID");
    }

    // EXCHANGE_RATE_INVALID: BaseCurrencyCode set but no rate
    [Fact]
    public void EXCHANGE_RATE_INVALID_missing_rate()
    {
        var result = Validate(r => r.BaseCurrencyCode = "USD");
        HasCode(result, "EXCHANGE_RATE_INVALID");
    }

    // EXCHANGE_RATE_INVALID: rate is zero
    [Fact]
    public void EXCHANGE_RATE_INVALID_zero()
    {
        var result = Validate(r => { r.BaseCurrencyCode = "USD"; r.ExchangeRate = 0m; });
        HasCode(result, "EXCHANGE_RATE_INVALID");
    }

    // EXCHANGE_RATE_INVALID: negative rate
    [Fact]
    public void EXCHANGE_RATE_INVALID_negative()
    {
        var result = Validate(r => { r.BaseCurrencyCode = "USD"; r.ExchangeRate = -1m; });
        HasCode(result, "EXCHANGE_RATE_INVALID");
    }

    // EXCHANGE_RATE: valid (both set, positive) → no error
    [Fact]
    public void Exchange_rate_valid_when_both_set()
    {
        var result = Validate(r => { r.BaseCurrencyCode = "GBP"; r.ExchangeRate = 1.27m; });
        result.Errors.Should().NotContain(e => e.Code == "EXCHANGE_RATE_INVALID");
    }

    // EMAIL_INVALID: no @ at all
    [Fact]
    public void EMAIL_INVALID_no_at()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "Acme", Email = "nodomain" });
        HasCode(result, "EMAIL_INVALID");
    }

    // EMAIL_INVALID: @ at start (nothing before it)
    [Fact]
    public void EMAIL_INVALID_at_start()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "Acme", Email = "@domain.com" });
        HasCode(result, "EMAIL_INVALID");
    }

    // EMAIL_INVALID: @ at end (nothing after it)
    [Fact]
    public void EMAIL_INVALID_at_end()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "Acme", Email = "user@" });
        HasCode(result, "EMAIL_INVALID");
    }

    // EMAIL: valid minimal address → no error
    [Fact]
    public void Email_valid_minimal()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "Acme", Email = "a@b" });
        result.Errors.Should().NotContain(e => e.Code == "EMAIL_INVALID");
    }

    // EMAIL: null → no error (optional field)
    [Fact]
    public void Email_null_is_valid()
    {
        var result = Validate(r => r.Customer = new CustomerInfo { Name = "Acme", Email = null });
        result.Errors.Should().NotContain(e => e.Code == "EMAIL_INVALID");
    }

    // Valid request → no errors at all.
    [Fact]
    public void Valid_request_has_no_errors()
        => InvoiceValidator.Validate(Valid()).IsValid.Should().BeTrue();

    // Multi-error: empty invoice number + empty line items → both codes present.
    [Fact]
    public void Multiple_errors_returned_together()
    {
        var result = Validate(r =>
        {
            r.InvoiceNumber = "";
            r.LineItems.Clear();
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVOICE_NUMBER_REQUIRED");
        result.Errors.Should().Contain(e => e.Code == "LINE_ITEMS_REQUIRED");
    }
}
