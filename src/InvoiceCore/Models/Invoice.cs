namespace InvoiceCore;

/// <summary>
/// An immutable invoice snapshot. All monetary totals are computed once at construction
/// and cached; the only mutable field is <see cref="Status"/>, which transitions through
/// <see cref="UpdateStatus"/>.
/// </summary>
public sealed class Invoice
{
    private readonly CalculationResult _calc;

    /// <summary>
    /// Constructs an invoice, computing all monetary totals immediately.
    /// </summary>
    /// <param name="lineItems">Line items. At least one required for a valid invoice.</param>
    /// <param name="taxRates">Tax rates applied at invoice level.</param>
    /// <param name="invoiceNumber">Unique invoice reference number.</param>
    /// <param name="issuedDate">Date the invoice is issued.</param>
    /// <param name="dueDate">Payment due date. Must not be before <paramref name="issuedDate"/>.</param>
    /// <param name="customer">Recipient details.</param>
    /// <param name="currencyCode">ISO-4217 currency code. Defaults to <c>"USD"</c>.</param>
    /// <param name="taxMode">Whether prices are tax-exclusive or tax-inclusive. Defaults to <see cref="TaxMode.Exclusive"/>.</param>
    /// <param name="discountPercent">Invoice-level discount percentage (0-100). Defaults to 0.</param>
    /// <param name="notes">Optional free-text notes.</param>
    /// <param name="id">Invoice identifier. A new <see cref="Guid"/> is generated when omitted.</param>
    /// <param name="baseCurrencyCode">Optional reporting currency code for exchange-rate conversion.</param>
    /// <param name="exchangeRate">
    /// Units of <paramref name="baseCurrencyCode"/> per one unit of <paramref name="currencyCode"/>.
    /// Required when <paramref name="baseCurrencyCode"/> is set.
    /// </param>
    /// <param name="taxCalculationMethod">
    /// Whether to apply tax to the aggregate subtotal or accumulate per-line rounded amounts.
    /// Defaults to <see cref="TaxCalculationMethod.SubtotalFirst"/>.
    /// <see cref="TaxCalculationMethod.PerLine"/> combined with <see cref="TaxMode.Inclusive"/>
    /// throws <see cref="NotSupportedException"/>.
    /// </param>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="taxCalculationMethod"/> is <see cref="TaxCalculationMethod.PerLine"/>
    /// and <paramref name="taxMode"/> is <see cref="TaxMode.Inclusive"/>. See SPEC.md §4.6.5.
    /// </exception>
    public Invoice(
        IReadOnlyList<LineItem> lineItems,
        IReadOnlyList<TaxRate> taxRates,
        string invoiceNumber,
        DateOnly issuedDate,
        DateOnly dueDate,
        CustomerInfo customer,
        string currencyCode = "USD",
        TaxMode taxMode = TaxMode.Exclusive,
        decimal discountPercent = 0m,
        string? notes = null,
        Guid? id = null,
        string? baseCurrencyCode = null,
        decimal? exchangeRate = null,
        TaxCalculationMethod taxCalculationMethod = TaxCalculationMethod.SubtotalFirst)
    {
        if (taxCalculationMethod == TaxCalculationMethod.PerLine && taxMode == TaxMode.Inclusive)
            throw new NotSupportedException(
                "TaxCalculationMethod.PerLine is not supported with TaxMode.Inclusive in this version. " +
                "See SPEC.md §4.6.5 for the mathematical reason this combination is deferred.");

        Id = id ?? Guid.NewGuid();
        InvoiceNumber = invoiceNumber;
        IssuedDate = issuedDate;
        DueDate = dueDate;
        Customer = customer;
        CurrencyCode = currencyCode;
        TaxMode = taxMode;
        TaxCalculationMethod = taxCalculationMethod;
        DiscountPercent = discountPercent;
        Notes = notes;
        BaseCurrencyCode = baseCurrencyCode;
        ExchangeRate = exchangeRate;
        TaxRates = taxRates.ToArray();

        var calcInput = new CalculationInput(
            lineItems.Select(l => new LineItemInput(l.Quantity, l.UnitPrice, l.DiscountPercent)).ToList(),
            taxRates.Select(r => new TaxRateInput(r.Name, r.Percentage)).ToList(),
            discountPercent,
            taxMode,
            currencyCode,
            taxCalculationMethod);

        _calc = InvoiceCalculator.Compute(calcInput);

        var items = new LineItem[lineItems.Count];
        for (var i = 0; i < lineItems.Count; i++)
        {
            items[i] = new LineItem
            {
                Description = lineItems[i].Description,
                Quantity = lineItems[i].Quantity,
                UnitPrice = lineItems[i].UnitPrice,
                DiscountPercent = lineItems[i].DiscountPercent,
                Total = _calc.LineTotals[i],
            };
        }
        LineItems = items;

        TaxBreakdown = _calc.TaxBreakdown
            .Select(t => new TaxLine { Name = t.Name, Percentage = t.Percentage, Amount = t.Amount })
            .ToArray();
    }

    /// <summary>Unique identifier for this invoice.</summary>
    public Guid Id { get; }

    /// <summary>Human-readable invoice reference number.</summary>
    public string InvoiceNumber { get; }

    /// <summary>Date the invoice was issued.</summary>
    public DateOnly IssuedDate { get; }

    /// <summary>Date by which payment is due.</summary>
    public DateOnly DueDate { get; }

    /// <summary>Current stored lifecycle status. Transitions via <see cref="UpdateStatus"/>.</summary>
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>ISO-4217 currency code. Defaults to <c>"USD"</c>.</summary>
    public string CurrencyCode { get; }

    /// <summary>Whether prices are tax-exclusive or tax-inclusive.</summary>
    public TaxMode TaxMode { get; }

    /// <summary>
    /// The method used to accumulate tax across line items. <see cref="TaxCalculationMethod.SubtotalFirst"/>
    /// applies tax once to the aggregate base; <see cref="TaxCalculationMethod.PerLine"/> rounds per
    /// line per rate and sums the results.
    /// </summary>
    public TaxCalculationMethod TaxCalculationMethod { get; }

    /// <summary>Recipient contact and tax details.</summary>
    public CustomerInfo Customer { get; }

    /// <summary>Line items with computed totals. Defensively copied at construction.</summary>
    public IReadOnlyList<LineItem> LineItems { get; }

    /// <summary>Tax rates applied at invoice level.</summary>
    public IReadOnlyList<TaxRate> TaxRates { get; }

    /// <summary>Invoice-level discount percentage (0-100).</summary>
    public decimal DiscountPercent { get; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; }

    /// <summary>Optional reporting currency code for exchange-rate rollup.</summary>
    public string? BaseCurrencyCode { get; }

    /// <summary>
    /// Units of <see cref="BaseCurrencyCode"/> per one unit of <see cref="CurrencyCode"/>.
    /// Stored but never applied; currency conversion is out of scope for v1.
    /// </summary>
    public decimal? ExchangeRate { get; }

    /// <summary>
    /// Tax-exclusive net subtotal. Always tax-exclusive in both <see cref="TaxMode"/> modes;
    /// in inclusive mode it is the back-calculated net.
    /// </summary>
    public decimal Subtotal => _calc.Subtotal;

    /// <summary>
    /// Invoice-level discount amount, rounded to currency precision.
    /// In <see cref="TaxCalculationMethod.PerLine"/> mode this is the sum of rounded per-line
    /// discount amounts, which may differ from <c>Round(Subtotal × DiscountPercent / 100)</c>
    /// by up to one minor unit per line.
    /// </summary>
    public decimal DiscountAmount => _calc.DiscountAmount;

    /// <summary>Sum of all per-rate tax amounts.</summary>
    public decimal TaxAmount => _calc.TaxAmount;

    /// <summary>Grand total: <see cref="Subtotal"/> minus <see cref="DiscountAmount"/> plus <see cref="TaxAmount"/>.</summary>
    public decimal Total => _calc.Total;

    /// <summary>Per-rate tax breakdown used for PDF rendering and filing.</summary>
    public IReadOnlyList<TaxLine> TaxBreakdown { get; }

    /// <summary>
    /// Returns <see langword="true"/> when <see cref="Status"/> is <see cref="InvoiceStatus.Sent"/>
    /// and <see cref="DueDate"/> is strictly before today according to <paramref name="clock"/>.
    /// Draft, Paid, and Cancelled invoices are never overdue.
    /// </summary>
    /// <param name="clock">Time source. Defaults to <see cref="TimeProvider.System"/>.</param>
    public bool IsOverdue(TimeProvider? clock = null)
    {
        if (Status != InvoiceStatus.Sent) return false;
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetLocalNow().DateTime);
        return DueDate < today;
    }

    /// <summary>
    /// Returns the effective display status. When <see cref="Status"/> is <see cref="InvoiceStatus.Sent"/>
    /// and the invoice <see cref="IsOverdue(TimeProvider?)"/>, returns <see cref="InvoiceStatus.Overdue"/>;
    /// otherwise returns the stored <see cref="Status"/>.
    /// </summary>
    /// <param name="clock">Time source. Defaults to <see cref="TimeProvider.System"/>.</param>
    public InvoiceStatus GetEffectiveStatus(TimeProvider? clock = null)
        => Status == InvoiceStatus.Sent && IsOverdue(clock)
            ? InvoiceStatus.Overdue
            : Status;

    /// <summary>
    /// Applies a status transition, mutates this instance, and returns it.
    /// Valid transitions: Draft→Sent, Draft→Cancelled, Sent→Paid, Sent→Cancelled.
    /// Paid and Cancelled are terminal. <see cref="InvoiceStatus.Overdue"/> is never settable.
    /// </summary>
    /// <param name="status">The target status.</param>
    /// <returns>This invoice (for chaining).</returns>
    /// <exception cref="InvalidStatusTransitionException">
    /// The requested transition is not permitted by the state machine.
    /// </exception>
    public Invoice UpdateStatus(InvoiceStatus status)
    {
        var allowed = (Status, status) switch
        {
            (InvoiceStatus.Draft, InvoiceStatus.Sent) => true,
            (InvoiceStatus.Draft, InvoiceStatus.Cancelled) => true,
            (InvoiceStatus.Sent, InvoiceStatus.Paid) => true,
            (InvoiceStatus.Sent, InvoiceStatus.Cancelled) => true,
            _ => false,
        };

        if (!allowed)
            throw new InvalidStatusTransitionException(Status, status);

        Status = status;
        return this;
    }
}
