using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Models;

public sealed class StatusMachineTests
{
    private static Invoice MakeInvoice(InvoiceStatus seed = InvoiceStatus.Draft)
    {
        var inv = new Invoice(
            lineItems: [new LineItem { Description = "X", Quantity = 1m, UnitPrice = 10m }],
            taxRates: [],
            invoiceNumber: "INV-001",
            issuedDate: new DateOnly(2025, 1, 1),
            dueDate: new DateOnly(2025, 2, 1),
            customer: new CustomerInfo { Name = "Acme" });

        if (seed == InvoiceStatus.Sent || seed == InvoiceStatus.Paid || seed == InvoiceStatus.Cancelled)
            inv.UpdateStatus(InvoiceStatus.Sent);
        if (seed == InvoiceStatus.Paid)
            inv.UpdateStatus(InvoiceStatus.Paid);
        if (seed == InvoiceStatus.Cancelled)
            inv.UpdateStatus(InvoiceStatus.Cancelled);
        return inv;
    }

    // Valid transitions — one test each.
    [Fact] public void Draft_to_Sent_succeeds()       => MakeInvoice(InvoiceStatus.Draft).UpdateStatus(InvoiceStatus.Sent).Status.Should().Be(InvoiceStatus.Sent);
    [Fact] public void Draft_to_Cancelled_succeeds()  => MakeInvoice(InvoiceStatus.Draft).UpdateStatus(InvoiceStatus.Cancelled).Status.Should().Be(InvoiceStatus.Cancelled);
    [Fact] public void Sent_to_Paid_succeeds()        => MakeInvoice(InvoiceStatus.Sent).UpdateStatus(InvoiceStatus.Paid).Status.Should().Be(InvoiceStatus.Paid);
    [Fact] public void Sent_to_Cancelled_succeeds()   => MakeInvoice(InvoiceStatus.Sent).UpdateStatus(InvoiceStatus.Cancelled).Status.Should().Be(InvoiceStatus.Cancelled);

    // UpdateStatus returns the same instance (chaining).
    [Fact]
    public void UpdateStatus_returns_same_instance()
    {
        var inv = MakeInvoice();
        inv.UpdateStatus(InvoiceStatus.Sent).Should().BeSameAs(inv);
    }

    // Invalid transitions — full matrix of (from, to) pairs not covered above.
    public static IEnumerable<object[]> InvalidTransitions =>
    [
        [InvoiceStatus.Draft,     InvoiceStatus.Draft],
        [InvoiceStatus.Draft,     InvoiceStatus.Paid],
        [InvoiceStatus.Draft,     InvoiceStatus.Overdue],
        [InvoiceStatus.Sent,      InvoiceStatus.Draft],
        [InvoiceStatus.Sent,      InvoiceStatus.Sent],
        [InvoiceStatus.Sent,      InvoiceStatus.Overdue],
        [InvoiceStatus.Paid,      InvoiceStatus.Draft],
        [InvoiceStatus.Paid,      InvoiceStatus.Sent],
        [InvoiceStatus.Paid,      InvoiceStatus.Paid],
        [InvoiceStatus.Paid,      InvoiceStatus.Cancelled],
        [InvoiceStatus.Paid,      InvoiceStatus.Overdue],
        [InvoiceStatus.Cancelled, InvoiceStatus.Draft],
        [InvoiceStatus.Cancelled, InvoiceStatus.Sent],
        [InvoiceStatus.Cancelled, InvoiceStatus.Paid],
        [InvoiceStatus.Cancelled, InvoiceStatus.Cancelled],
        [InvoiceStatus.Cancelled, InvoiceStatus.Overdue],
    ];

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Invalid_transition_throws(InvoiceStatus from, InvoiceStatus to)
    {
        var inv = MakeInvoice(from);
        var act = () => inv.UpdateStatus(to);
        act.Should().Throw<InvalidStatusTransitionException>()
            .Which.From.Should().Be(from);
        act.Should().Throw<InvalidStatusTransitionException>()
            .Which.To.Should().Be(to);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Invalid_transition_leaves_status_unchanged(InvoiceStatus from, InvoiceStatus to)
    {
        var inv = MakeInvoice(from);
        try { inv.UpdateStatus(to); } catch (InvalidStatusTransitionException) { }
        inv.Status.Should().Be(from);
    }
}
