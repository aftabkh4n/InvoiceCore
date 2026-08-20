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

## JSON export

`ExportToJson` returns a camelCase JSON string. Two options control the output:

| Option | Default | Effect |
|---|---|---|
| `MoneyFormat` | `MoneyFormat.String` | Monetary amounts as quoted decimal strings (`"10.50"`) |
| `IncludeNulls` | `false` | Omit `null` optional fields |

**Default output (MoneyFormat.String, IncludeNulls=false):**

```json
{
  "invoiceNumber": "INV-001",
  "currencyCode": "USD",
  "subtotal": "100.00",
  "taxAmount": "20.00",
  "total": "120.00",
  "lineItems": [{ "unitPrice": "50.00", "total": "100.00", ... }],
  ...
}
```

Monetary strings are formatted to the currency's minor-unit precision using
`InvariantCulture`: JPY produces `"1000"`, KWD produces `"100.010"`. Percentages,
quantities, and `exchangeRate` stay as JSON numbers.

**Legacy numeric output (restores pre-0.3.0 behaviour):**

```csharp
var json = svc.ExportToJson(invoice, new JsonExportOptions
{
    MoneyFormat  = MoneyFormat.Number,
    IncludeNulls = true,
});
```

> **Breaking change in 0.3.0:** The defaults changed from `MoneyFormat.Number +
> IncludeNulls=true` to `MoneyFormat.String + IncludeNulls=false`. Consumers
> that parse monetary fields as numbers or compare exact JSON byte-for-byte must
> update their code.

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

## Tax compliance

InvoiceCore's rounding has been validated against two published tax authority sources.
Full comparison tables and source links are in [docs/TAX-CONFORMANCE.md](docs/TAX-CONFORMANCE.md).

### Rounding rule

[HMRC VATREC12030](https://www.gov.uk/hmrc-internal-manuals/vat-trader-records/vatrec12030)
and [ATO GSTA 1999 s9-90](https://classic.austlii.edu.au/au/legis/cth/consol_act/antsasta1999402/s9.90.html)
both specify the same rule: round to the nearest minor unit, half-up at the midpoint.
InvoiceCore uses `MidpointRounding.AwayFromZero`, which matches this exactly for positive amounts.

### Tax applied to the rounded subtotal, not per line

InvoiceCore applies the tax rate to the rounded subtotal. HMRC and the ATO both permit
per-line rounding, which can differ by one minor unit on multi-line invoices. Example at 20% UK VAT:

| Method | Tax | Total |
|---|---|---|
| InvoiceCore: `Round(£5.01 × 0.20)` | £1.00 | £6.01 |
| HMRC per-line (also permitted): `3 × Round(£1.67 × 0.20)` | £0.99 | £6.00 |

Both are acceptable under HMRC guidance. InvoiceCore does not offer a per-line mode.

### HMRC truncation concession (Notice 700 §17.5) — not implemented

HMRC permits invoice traders to optionally round total VAT **down** to the nearest penny
(truncation, not half-up). InvoiceCore does not implement this. Callers who need it must
post-process `TaxAmount`. The concession is relevant only at the UK 5% reduced rate on
specific net values — for example, 5% of £0.30 = £0.015 exactly: InvoiceCore gives £0.02,
the concession allows £0.01. No divergence is possible at the standard 20% rate on
whole-penny net values, because 20% of any integer number of pence is never exactly 0.5p.

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
| `InvoiceCore` | v0.3.0 (current) |
| `InvoiceCore.Pdf` | Planned |
| `InvoiceCore.EfCore` | Planned |
| `InvoiceCore.Blazor` | Planned |

---

## License

MIT © 2026 Aftab Bashir
