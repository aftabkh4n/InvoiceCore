using System.Text.RegularExpressions;

namespace InvoiceCore;

/// <summary>
/// Validates a <see cref="CreateInvoiceRequest"/> against all rules in SPEC §7.
/// Returns a <see cref="ValidationResult"/>; never throws.
/// </summary>
public static class InvoiceValidator
{
    private static readonly Regex s_currencyPattern =
        new(@"^[A-Z]{3}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    /// <summary>Validates <paramref name="request"/> and returns the result.</summary>
    /// <param name="request">The request to validate.</param>
    public static ValidationResult Validate(CreateInvoiceRequest request)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            errors.Add(new("InvoiceNumber", "Invoice number is required.", "INVOICE_NUMBER_REQUIRED"));

        if (request.Customer is null || string.IsNullOrWhiteSpace(request.Customer.Name))
            errors.Add(new("Customer", "Customer with a non-empty name is required.", "CUSTOMER_REQUIRED"));

        if (request.DueDate < request.IssuedDate)
            errors.Add(new("DueDate", "Due date must not be before the issued date.", "DUE_BEFORE_ISSUE"));

        if (!s_currencyPattern.IsMatch(request.CurrencyCode ?? string.Empty))
            errors.Add(new("CurrencyCode", "Currency code must be exactly three uppercase ASCII letters.", "CURRENCY_INVALID"));

        if (request.LineItems.Count == 0)
            errors.Add(new("LineItems", "At least one line item is required.", "LINE_ITEMS_REQUIRED"));

        for (var i = 0; i < request.LineItems.Count; i++)
        {
            var item = request.LineItems[i];
            var path = $"LineItems[{i}]";

            if (string.IsNullOrWhiteSpace(item.Description))
                errors.Add(new($"{path}.Description", "Line description is required.", "LINE_DESCRIPTION_REQUIRED"));

            if (item.Quantity <= 0m)
                errors.Add(new($"{path}.Quantity", "Quantity must be greater than zero.", "LINE_QUANTITY_INVALID"));

            if (item.UnitPrice < 0m)
                errors.Add(new($"{path}.UnitPrice", "Unit price must be zero or greater.", "LINE_UNIT_PRICE_INVALID"));

            if (item.DiscountPercent < 0m || item.DiscountPercent > 100m)
                errors.Add(new($"{path}.DiscountPercent", "Line discount must be between 0 and 100.", "LINE_DISCOUNT_RANGE"));
        }

        if (request.DiscountPercent < 0m || request.DiscountPercent > 100m)
            errors.Add(new("DiscountPercent", "Invoice discount must be between 0 and 100.", "INVOICE_DISCOUNT_RANGE"));

        var seenTaxNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var j = 0; j < request.TaxRates.Count; j++)
        {
            var rate = request.TaxRates[j];
            var path = $"TaxRates[{j}]";

            if (string.IsNullOrWhiteSpace(rate.Name))
                errors.Add(new($"{path}.Name", "Tax rate name is required.", "TAX_NAME_REQUIRED"));

            if (rate.Percentage < 0m || rate.Percentage > 100m)
                errors.Add(new($"{path}.Percentage", "Tax percentage must be between 0 and 100.", "TAX_PERCENTAGE_RANGE"));

            if (!string.IsNullOrWhiteSpace(rate.Name) && !seenTaxNames.Add(rate.Name))
                errors.Add(new($"{path}.Name", $"Duplicate tax rate name '{rate.Name}' (case-insensitive).", "TAX_DUPLICATE_NAME"));
        }

        if (request.ExchangeRate.HasValue || request.BaseCurrencyCode is not null)
        {
            if (!request.ExchangeRate.HasValue || request.ExchangeRate.Value <= 0m || request.BaseCurrencyCode is null)
                errors.Add(new("ExchangeRate", "ExchangeRate must be positive and BaseCurrencyCode must be set together.", "EXCHANGE_RATE_INVALID"));
        }

        if (request.Customer?.Email is { } email)
        {
            var at = email.IndexOf('@');
            if (at <= 0 || at >= email.Length - 1)
                errors.Add(new("Customer.Email", "Email address must contain '@' with characters on both sides.", "EMAIL_INVALID"));
        }

        return new ValidationResult(errors);
    }
}
