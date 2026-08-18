# InvoiceCore

> Zero-dependency invoicing primitives for .NET. Predictable, documented rounding,
> multi-rate tax (inclusive and exclusive), status rules, JSON/CSV export.
> Bring your own storage and rendering.

[![NuGet](https://img.shields.io/nuget/v/InvoiceCore.svg)](https://www.nuget.org/packages/InvoiceCore)
[![CI](https://github.com/aftabkh4n/InvoiceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/InvoiceCore/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Quick start

<!-- Compiled verbatim as a test: tests/InvoiceCore.Tests/ReadmeExamplesTests.cs -->
```csharp
using System;                      // Console, DateOnly
using System.Collections.Generic;  // List<T>
using InvoiceCore;                 // InvoiceService, CreateInvoiceRequest, CustomerInfo, LineItem, TaxRate

var svc = new InvoiceService();

var invoice = svc.Create(new CreateInvoiceRequest
{
    InvoiceNumber = "INV-001",
    IssuedDate    = new DateOnly(2025, 1, 15),
    DueDate       = new DateOnly(2025, 2, 15),
    CurrencyCode  = "USD",
    Customer      = new CustomerInfo { Name = "Acme Corp" },
    LineItems     = new List<LineItem> { new LineItem { Description = "Consulting", Quantity = 2, UnitPrice = 50m } },
    TaxRates      = new List<TaxRate>  { new TaxRate  { Name = "VAT", Percentage = 20m } },
});

Console.WriteLine($"Subtotal : {invoice.Subtotal:C}");   // $100.00
Console.WriteLine($"VAT 20%  : {invoice.TaxAmount:C}");  // $20.00
Console.WriteLine($"Total    : {invoice.Total:C}");       // $120.00
Console.WriteLine(svc.ExportToJson(invoice));
```

---

## Tax arithmetic: worked example

Both modes produce the same correctly-rounded totals. `Subtotal` is **always
tax-exclusive**: this is the value most likely to surprise callers switching
between modes.

### Exclusive mode (prices are net)

```
Line:  Qty 2 × $50.00 = $100.00
                         ───────
Subtotal (net)           $100.00   ← tax-exclusive in both modes
VAT 20%  on $100.00    =  $20.00
                         ───────
Total                    $120.00
```

### Inclusive mode (same gross price, tax extracted)

```csharp
using InvoiceCore; // TaxMode

// TaxMode = TaxMode.Inclusive, one line Qty 1 × $120.00, VAT 20%
```

```
Line total (gross):      $120.00
Subtotal  = Round($120.00 / 1.20) =  $100.00   ← tax-exclusive net
VAT 20%   = Round($100.00 × 0.20) =  $ 20.00
Residual reconciliation keeps Total = $120.00 exactly
                                      ───────
Total                                 $120.00
```

> **Note:** `Subtotal` is the tax-exclusive net in **both** modes. In Inclusive
> mode it is the back-calculated base, not the gross line-item figure.

Multiple rates are additive, never compounded:

```
Subtotal              $1 000.00
VAT 20%    $200.00
Levy  5%   $ 50.00
           ───────
TaxAmount             $  250.00
Total                 $1 250.00
```

---

## Rounding policy

All money arithmetic uses **`MidpointRounding.AwayFromZero`** (half-up), routed
exclusively through a single internal `Money.Round` call site. There are no
ad-hoc `Math.Round` or `decimal.Round` calls anywhere in the library.

Minor-unit precision is resolved per ISO-4217:

| Digits | Codes (examples) |
|--------|-----------------|
| 0 | JPY, KRW, VND |
| 3 | KWD, BHD, OMR |
| 2 | USD, EUR, GBP and everything else |

Unknown codes default to 2 digits and never throw.

---

## What this is not

- **Not a payment processor.** No partial payments, payment allocation, or credit
  notes. Negative quantities are rejected in v1.
- **Not a PDF renderer.** Use `InvoiceCore.Pdf` (planned) for that.
- **Not an accounting ledger.** No bank feeds, journal entries, or chart of
  accounts.
- **Not a tax jurisdiction lookup.** You supply the rate; InvoiceCore applies it
  correctly.
- **Not an e-invoicing compliance layer.** Peppol, ZATCA, and FatturaPA are
  out of scope.
- **Not a recurring-invoice engine.** No schedules, templates, or auto-numbering.
- **Not a currency converter.** An exchange rate can be stored on an invoice for
  reporting grouping; it is never applied.
- **Not a per-line tax engine.** Tax rates apply at invoice level in v1. Line
  items with differing VAT rates are not supported yet.

---

## How this was built

See [docs/PROCESS.md](docs/PROCESS.md). The test suite was adversarially verified
by mutation testing, which found four defects a green 202-test suite could not see.

---

## Prior art

| Library | What it does | Why InvoiceCore is different |
|---|---|---|
| `InvoiceSdk` | Fluent PDF invoicing (.NET 6) | Depends on QuestPDF + ServiceStack.Text |
| `InvoicerNETCore`, `Invoicer`, `InvoiceGenerator.Core` | PDF generators | Not model libraries |

InvoiceCore replaces the 400 lines of subtly-wrong tax arithmetic every SaaS
team writes by hand, with no dependencies, no rendering, and documented,
tested rounding behaviour.

---

## Roadmap

| Package | Status |
|---|---|
| `InvoiceCore` | v0.1.1 (current) |
| `InvoiceCore.Pdf` | Planned |
| `InvoiceCore.EfCore` | Planned |
| `InvoiceCore.Blazor` | Planned |

---

## License

MIT © 2026 Aftab Bashir
