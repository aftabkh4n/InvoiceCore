using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Models;

public sealed class ImmutabilityTests
{
    [Fact]
    public void Mutating_original_lineItems_list_does_not_change_invoice()
    {
        var items = new List<LineItem>
        {
            new() { Description = "Widget", Quantity = 2m, UnitPrice = 50m },
        };
        var inv = new Invoice(items, [], "INV-001",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        var countBefore = inv.LineItems.Count;
        var totalBefore = inv.Total;

        // Mutate the source list after construction.
        items.Add(new LineItem { Description = "Extra", Quantity = 1m, UnitPrice = 999m });

        inv.LineItems.Count.Should().Be(countBefore);
        inv.Total.Should().Be(totalBefore);
    }

    [Fact]
    public void Mutating_original_taxRates_list_does_not_change_invoice()
    {
        var rates = new List<TaxRate>
        {
            new() { Name = "VAT", Percentage = 20m },
        };
        var inv = new Invoice(
            [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 100m }],
            rates, "INV-001",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        var taxBefore = inv.TaxAmount;

        // Mutate the source list after construction.
        rates.Add(new TaxRate { Name = "GST", Percentage = 10m });

        inv.TaxAmount.Should().Be(taxBefore);
        inv.TaxRates.Count.Should().Be(1);
    }

    [Fact]
    public void LineItems_collection_is_not_castable_to_mutable_list()
    {
        var inv = new Invoice(
            [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            [], "INV-001",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        // The underlying store is an array. Casting to List<LineItem> returns null.
        var asList = inv.LineItems as List<LineItem>;
        asList.Should().BeNull();
    }

    [Fact]
    public void TaxRates_collection_is_not_castable_to_mutable_list()
    {
        var inv = new Invoice(
            [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            [new TaxRate { Name = "VAT", Percentage = 10m }],
            "INV-001",
            new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1),
            new CustomerInfo { Name = "Acme" });

        var asList = inv.TaxRates as List<TaxRate>;
        asList.Should().BeNull();
    }
}
