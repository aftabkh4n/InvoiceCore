namespace InvoiceCore;

// Snapshot DTOs used only for JSON serialisation. Internal because callers
// receive the serialised string; they never need to construct or inspect these.

internal sealed class CustomerInfoDto
{
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? TaxNumber { get; init; }
}

internal sealed class LineItemDto
{
    public string Description { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal Total { get; init; }
}

internal sealed class TaxRateDto
{
    public string Name { get; init; } = "";
    public decimal Percentage { get; init; }
}

internal sealed class TaxLineDto
{
    public string Name { get; init; } = "";
    public decimal Percentage { get; init; }
    public decimal Amount { get; init; }
}

internal sealed class InvoiceDto
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public DateOnly IssuedDate { get; init; }
    public DateOnly DueDate { get; init; }
    public InvoiceStatus Status { get; init; }
    public string CurrencyCode { get; init; } = "";
    public TaxMode TaxMode { get; init; }
    public CustomerInfoDto Customer { get; init; } = new();
    public LineItemDto[] LineItems { get; init; } = [];
    public TaxRateDto[] TaxRates { get; init; } = [];
    public decimal DiscountPercent { get; init; }
    public string? Notes { get; init; }
    public string? BaseCurrencyCode { get; init; }
    public decimal? ExchangeRate { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal Total { get; init; }
    public TaxLineDto[] TaxBreakdown { get; init; } = [];

    internal static InvoiceDto From(Invoice inv) => new()
    {
        Id = inv.Id,
        InvoiceNumber = inv.InvoiceNumber,
        IssuedDate = inv.IssuedDate,
        DueDate = inv.DueDate,
        Status = inv.Status,
        CurrencyCode = inv.CurrencyCode,
        TaxMode = inv.TaxMode,
        Customer = new CustomerInfoDto
        {
            Name = inv.Customer.Name,
            Email = inv.Customer.Email,
            Address = inv.Customer.Address,
            TaxNumber = inv.Customer.TaxNumber,
        },
        LineItems = inv.LineItems.Select(l => new LineItemDto
        {
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DiscountPercent = l.DiscountPercent,
            Total = l.Total,
        }).ToArray(),
        TaxRates = inv.TaxRates.Select(r => new TaxRateDto
        {
            Name = r.Name,
            Percentage = r.Percentage,
        }).ToArray(),
        DiscountPercent = inv.DiscountPercent,
        Notes = inv.Notes,
        BaseCurrencyCode = inv.BaseCurrencyCode,
        ExchangeRate = inv.ExchangeRate,
        Subtotal = inv.Subtotal,
        DiscountAmount = inv.DiscountAmount,
        TaxAmount = inv.TaxAmount,
        Total = inv.Total,
        TaxBreakdown = inv.TaxBreakdown.Select(t => new TaxLineDto
        {
            Name = t.Name,
            Percentage = t.Percentage,
            Amount = t.Amount,
        }).ToArray(),
    };
}
