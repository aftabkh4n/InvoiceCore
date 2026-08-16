// InvoiceCore sample: create an invoice with mixed tax rates,
// print the breakdown, transition status, then export to JSON and CSV.

using System.Globalization;
using InvoiceCore;

var svc = new InvoiceService();

// ── 1. Build the request ───────────────────────────────────────────────────
var request = new CreateInvoiceRequest
{
    InvoiceNumber = "INV-2025-001",
    IssuedDate    = new DateOnly(2025, 3, 1),
    DueDate       = new DateOnly(2025, 3, 31),
    CurrencyCode  = "USD",
    Customer      = new CustomerInfo
    {
        Name  = "Acme Corp",
        Email = "accounts@acme.example",
    },
    LineItems =
    [
        new LineItem { Description = "Software development", Quantity = 5,  UnitPrice = 400m },
        new LineItem { Description = "Cloud hosting",        Quantity = 1,  UnitPrice = 120m },
        new LineItem { Description = "Support retainer",     Quantity = 12, UnitPrice =  25m },
    ],
    // Two independent rates, applied additively, never compounded.
    TaxRates =
    [
        new TaxRate { Name = "VAT",            Percentage = 20m },
        new TaxRate { Name = "Service charge", Percentage =  5m },
    ],
};

// ── 2. Create the invoice (validates, computes all totals) ─────────────────
Invoice invoice = svc.Create(request);

// ── 3. Print the breakdown using InvoicePresentation ──────────────────────
var view = InvoicePresentation.From(invoice, CultureInfo.GetCultureInfo("en-US"));

Console.WriteLine($"Invoice  : {view.InvoiceNumber}");
Console.WriteLine($"Customer : {view.CustomerName}");
Console.WriteLine($"Status   : {view.Status}");
Console.WriteLine($"Due      : {view.DueDate}");
Console.WriteLine();

Console.WriteLine("Lines:");
foreach (var line in view.LineItems)
    Console.WriteLine($"  {line.Description,-26} {line.Quantity,3} × {line.UnitPrice,10}  = {line.Total,12}");

Console.WriteLine();
Console.WriteLine($"  {"Subtotal",-38} {view.Subtotal,12}");
foreach (var tax in view.TaxBreakdown)
    Console.WriteLine($"  {tax.Label,-38} {tax.Amount,12}");
Console.WriteLine($"  {"─────────────────────────────────────────────────────",-51}");
Console.WriteLine($"  {"Total",-38} {view.Total,12}");

// ── 4. Transition to Sent ──────────────────────────────────────────────────
svc.UpdateStatus(invoice, InvoiceStatus.Sent);
Console.WriteLine($"\nStatus after send : {invoice.Status}");

// ── 5. JSON export ─────────────────────────────────────────────────────────
Console.WriteLine("\n── JSON (indented) " + new string('─', 50));
Console.WriteLine(svc.ExportToJson(invoice, new JsonExportOptions { Indented = true }));

// ── 6. CSV export ─────────────────────────────────────────────────────────
Console.WriteLine("\n── CSV (invoice-level) " + new string('─', 46));
Console.Write(svc.ExportToCsv([invoice]));

Console.WriteLine("\n── CSV (line-item detail) " + new string('─', 43));
Console.Write(svc.ExportLineItemsToCsv([invoice]));
