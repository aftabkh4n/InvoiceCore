using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace InvoiceCore.Tests.Models;

public sealed class OverdueTests
{
    private static readonly DateOnly DueDate = new(2025, 6, 15);

    // Clock frozen one day before due date: not overdue.
    private static FakeTimeProvider Before => new(new DateTimeOffset(2025, 6, 14, 0, 0, 0, TimeSpan.Zero));
    // Clock frozen on due date: not overdue (strictly less than).
    private static FakeTimeProvider OnDate => new(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero));
    // Clock frozen one day after due date: overdue.
    private static FakeTimeProvider After => new(new DateTimeOffset(2025, 6, 16, 0, 0, 0, TimeSpan.Zero));

    private static Invoice MakeSent()
    {
        var inv = new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [],
            invoiceNumber: "INV-001",
            issuedDate: new DateOnly(2025, 1, 1),
            dueDate: DueDate,
            customer: new CustomerInfo { Name = "Acme" });
        inv.UpdateStatus(InvoiceStatus.Sent);
        return inv;
    }

    // Sent + before due date → not overdue.
    [Fact]
    public void Sent_before_due_date_is_not_overdue()
        => MakeSent().IsOverdue(Before).Should().BeFalse();

    // Sent + on due date → not overdue (boundary: DueDate < today is false when equal).
    [Fact]
    public void Sent_on_due_date_is_not_overdue()
        => MakeSent().IsOverdue(OnDate).Should().BeFalse();

    // Sent + after due date → overdue.
    [Fact]
    public void Sent_after_due_date_is_overdue()
        => MakeSent().IsOverdue(After).Should().BeTrue();

    // GetEffectiveStatus reflects Overdue only when Sent + after due date.
    [Fact]
    public void GetEffectiveStatus_returns_Overdue_when_past_due()
        => MakeSent().GetEffectiveStatus(After).Should().Be(InvoiceStatus.Overdue);

    [Fact]
    public void GetEffectiveStatus_returns_Sent_on_due_date()
        => MakeSent().GetEffectiveStatus(OnDate).Should().Be(InvoiceStatus.Sent);

    [Fact]
    public void GetEffectiveStatus_returns_Sent_before_due_date()
        => MakeSent().GetEffectiveStatus(Before).Should().Be(InvoiceStatus.Sent);

    // Draft is never overdue regardless of date.
    [Theory]
    [InlineData(nameof(Before))]
    [InlineData(nameof(OnDate))]
    [InlineData(nameof(After))]
    public void Draft_is_never_overdue(string clockName)
    {
        var clock = clockName == nameof(After) ? After : clockName == nameof(OnDate) ? OnDate : Before;
        var inv = new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [],
            invoiceNumber: "INV-001",
            issuedDate: new DateOnly(2025, 1, 1),
            dueDate: DueDate,
            customer: new CustomerInfo { Name = "Acme" });
        inv.IsOverdue(clock).Should().BeFalse();
        inv.GetEffectiveStatus(clock).Should().Be(InvoiceStatus.Draft);
    }

    // Paid is never overdue.
    [Fact]
    public void Paid_is_never_overdue()
    {
        var inv = MakeSent();
        inv.UpdateStatus(InvoiceStatus.Paid);
        inv.IsOverdue(After).Should().BeFalse();
        inv.GetEffectiveStatus(After).Should().Be(InvoiceStatus.Paid);
    }

    // Cancelled is never overdue.
    [Fact]
    public void Cancelled_is_never_overdue()
    {
        var inv = MakeSent();
        inv.UpdateStatus(InvoiceStatus.Cancelled);
        inv.IsOverdue(After).Should().BeFalse();
        inv.GetEffectiveStatus(After).Should().Be(InvoiceStatus.Cancelled);
    }
}
