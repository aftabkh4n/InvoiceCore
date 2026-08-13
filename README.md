# InvoiceCore

> Zero-dependency invoicing primitives for .NET. Correct decimal money maths,
> multi-rate tax (inclusive and exclusive), status rules, JSON/CSV export.
> Bring your own storage and rendering.

[![NuGet](https://img.shields.io/nuget/v/InvoiceCore.svg)](https://www.nuget.org/packages/InvoiceCore)
[![CI](https://github.com/aftabkh4n/InvoiceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/aftabkh4n/InvoiceCore/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Quick start

> **Note:** `InvoiceService` and the supporting models are implemented in Phase 3.
> This stub will be replaced with a working 10-line example above the fold.

```csharp
// Coming in v1.0.0 — see SPEC.md Phase 3
```

---

## Rounding policy

All money arithmetic uses `MidpointRounding.AwayFromZero` (half-up), routed
exclusively through the internal `Money.Round` helper. No ad-hoc
`Math.Round` or `decimal.Round` calls anywhere in the library.

Minor-unit precision is looked up per ISO-4217 code (0 digits for JPY/KRW,
3 for KWD/BHD, 2 for everything else). Unknown codes default to 2 and do not
throw.

## Important: `Subtotal` is always tax-exclusive

In both `TaxMode.Exclusive` and `TaxMode.Inclusive`, `Subtotal` is the
**tax-exclusive net amount**. In inclusive mode the line totals already include
tax, so `Subtotal` is the back-calculated base — this is the value most likely
to surprise callers switching between modes.

---

## What this is not

- **Not a payment processor.** No partial payments, payment allocation, or
  credit notes.
- **Not a PDF renderer.** Use `InvoiceCore.Pdf` (planned) for that.
- **Not an accounting ledger.** No bank feeds, journal entries, or chart of
  accounts.
- **Not a tax jurisdiction lookup.** You supply the rate; InvoiceCore applies it
  correctly.
- **Not an e-invoicing compliance layer.** Peppol, ZATCA, FatturaPA are out of
  scope.

---

## Prior art

| Library | What it does | Why InvoiceCore is different |
|---|---|---|
| `InvoiceSdk` | Fluent PDF invoicing (.NET 6) | Depends on QuestPDF + ServiceStack.Text |
| `InvoicerNETCore`, `Invoicer`, `InvoiceGenerator.Core` | PDF generators | Not model libraries |

InvoiceCore replaces the 400 lines of subtly-wrong tax arithmetic every SaaS
team writes by hand — with no dependencies, no rendering, and documented,
tested rounding behaviour.

---

## Roadmap

| Package | Status |
|---|---|
| `InvoiceCore` | v1.0.0 in development |
| `InvoiceCore.Pdf` | Planned |
| `InvoiceCore.EfCore` | Planned |
| `InvoiceCore.Blazor` | Planned |

---

## License

MIT © 2026 Aftab Bashir
