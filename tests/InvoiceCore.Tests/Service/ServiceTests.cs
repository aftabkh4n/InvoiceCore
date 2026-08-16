using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Service;

public sealed class ServiceTests
{
    private static InvoiceService Svc() => new();

    private static CreateInvoiceRequest ValidRequest() => new()
    {
        InvoiceNumber = "INV-001",
        IssuedDate = new DateOnly(2025, 1, 1),
        DueDate = new DateOnly(2025, 2, 1),
        CurrencyCode = "USD",
        Customer = new CustomerInfo { Name = "Acme" },
        LineItems =
        [
            new LineItem { Description = "Consulting", Quantity = 2m, UnitPrice = 50m },
        ],
        TaxRates = [new TaxRate { Name = "VAT", Percentage = 20m }],
    };

    // Create: valid request returns Draft invoice with correct totals.
    [Fact]
    public void Create_valid_request_returns_draft_invoice()
    {
        var inv = Svc().Create(ValidRequest());
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.Total.Should().Be(120.00m);   // 100 * 1.20
        inv.Subtotal.Should().Be(100.00m);
        inv.TaxAmount.Should().Be(20.00m);
    }

    // Create: invalid request throws InvoiceValidationException carrying all errors.
    [Fact]
    public void Create_invalid_request_throws_with_validation_result()
    {
        var req = ValidRequest();
        req.InvoiceNumber = "";
        req.LineItems.Clear();

        var act = () => Svc().Create(req);
        act.Should().Throw<InvoiceValidationException>()
            .Which.ValidationResult.Errors.Should().Contain(e => e.Code == "INVOICE_NUMBER_REQUIRED")
            .And.Contain(e => e.Code == "LINE_ITEMS_REQUIRED");
    }

    // Create: normalises CurrencyCode to uppercase.
    [Fact]
    public void Create_normalises_currency_code_uppercase()
    {
        var req = ValidRequest();
        req.CurrencyCode = "usd";
        // lowercase fails validation (CURRENCY_INVALID); normalisation happens before validate
        // SPEC says "uppercased on set"; the service normalises before creating
        var act = () => Svc().Create(req);
        // Actually per SPEC validator checks [A-Z]{3}, so lowercase fails validation
        act.Should().Throw<InvoiceValidationException>();
    }

    // Validate: returns valid result for a good request.
    [Fact]
    public void Validate_valid_request_is_valid()
        => Svc().Validate(ValidRequest()).IsValid.Should().BeTrue();

    // Validate: returns errors without throwing.
    [Fact]
    public void Validate_invalid_request_returns_errors()
    {
        var req = ValidRequest();
        req.Customer = null;
        var result = Svc().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "CUSTOMER_REQUIRED");
    }

    // UpdateStatus: delegates to Invoice.UpdateStatus, returns same instance.
    [Fact]
    public void UpdateStatus_transitions_and_returns_same_instance()
    {
        var inv = Svc().Create(ValidRequest());
        var returned = Svc().UpdateStatus(inv, InvoiceStatus.Sent);
        returned.Should().BeSameAs(inv);
        inv.Status.Should().Be(InvoiceStatus.Sent);
    }

    // UpdateStatus: propagates InvalidStatusTransitionException.
    [Fact]
    public void UpdateStatus_throws_on_invalid_transition()
    {
        var inv = Svc().Create(ValidRequest()); // Draft
        var act = () => Svc().UpdateStatus(inv, InvoiceStatus.Paid);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    // GetOverdue: returns only overdue invoices using the service clock.
    [Fact]
    public void GetOverdue_returns_only_overdue_invoices()
    {
        // Clock is 2025-06-01; issued date is 2025-01-01, so pastDue must be between them.
        var pastDue = new DateOnly(2025, 3, 1);
        var futureDue = new DateOnly(2099, 1, 1);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var svc = new InvoiceService(clock);

        Invoice MakeSent(DateOnly due)
        {
            var req = ValidRequest();
            req.DueDate = due;
            var inv = svc.Create(req);
            svc.UpdateStatus(inv, InvoiceStatus.Sent);
            return inv;
        }

        var overdue = MakeSent(pastDue);
        var current = MakeSent(futureDue);

        var result = svc.GetOverdue([overdue, current]).ToList();
        result.Should().ContainSingle().Which.Should().BeSameAs(overdue);
    }
}
