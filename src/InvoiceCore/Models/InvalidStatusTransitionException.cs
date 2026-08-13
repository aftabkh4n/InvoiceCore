namespace InvoiceCore;

/// <summary>
/// Thrown when an <see cref="Invoice"/> status transition is not permitted by the state machine,
/// or when <see cref="InvoiceStatus.Overdue"/> is passed as a target (it is derived, never stored).
/// </summary>
public sealed class InvalidStatusTransitionException : Exception
{
    /// <summary>The status the invoice was in when the transition was attempted.</summary>
    public InvoiceStatus From { get; }

    /// <summary>The target status that was requested.</summary>
    public InvoiceStatus To { get; }

    /// <summary>
    /// Initialises a new <see cref="InvalidStatusTransitionException"/>.
    /// </summary>
    /// <param name="from">The current stored status of the invoice.</param>
    /// <param name="to">The status that was requested.</param>
    public InvalidStatusTransitionException(InvoiceStatus from, InvoiceStatus to)
        : base($"Cannot transition from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}
