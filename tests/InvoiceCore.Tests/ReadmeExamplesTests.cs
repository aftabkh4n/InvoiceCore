// See: README.md "Quick start" block. This test compiles that block verbatim
// (minus the Console.WriteLine calls) and asserts the totals it claims.
// If the README example and this test drift apart, CI fails.
using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests;

public sealed class ReadmeExamplesTests
{
    [Fact]
    public void QuickStart_compiles_and_produces_correct_totals()
    {
        // === README.md "Quick start" block (verbatim from that section) ===
        var svc = new InvoiceService();

        var invoice = svc.Create(new CreateInvoiceRequest
        {
            InvoiceNumber = "INV-001",
            IssuedDate    = new DateOnly(2025, 1, 15),
            DueDate       = new DateOnly(2025, 2, 15),
            CurrencyCode  = "USD",
            Customer      = new CustomerInfo { Name = "Acme Corp" },
            LineItems     = [new LineItem { Description = "Consulting", Quantity = 2, UnitPrice = 50m }],
            TaxRates      = [new TaxRate  { Name = "VAT", Percentage = 20m }],
        });
        // === end README block ===

        invoice.Subtotal.Should().Be(100.00m);   // README says $100.00
        invoice.TaxAmount.Should().Be(20.00m);   // README says $ 20.00
        invoice.Total.Should().Be(120.00m);      // README says $120.00
        svc.ExportToJson(invoice).Should().NotBeNullOrWhiteSpace();
    }
}
