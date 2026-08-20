# Tax conformance notes

This document records how InvoiceCore's arithmetic compares to two published tax authority
sources. It is a factual record, not a compliance certificate. No claim is made that using
InvoiceCore satisfies any jurisdiction's filing obligations.

Findings were produced by running InvoiceCore against boundary cases derived from the stated
rules. The test file `tests/InvoiceCore.Tests/TaxAuthorityConformanceTests.cs` is the
regression guard for everything documented here.

---

## Sources

| Authority | Document | URL |
|---|---|---|
| HMRC (UK) | VAT Traders Records Manual VATREC12030 — What do we mean by rounding up and down? | https://www.gov.uk/hmrc-internal-manuals/vat-trader-records/vatrec12030 |
| HMRC (UK) | VAT Traders Records Manual VATREC12010 — What is the rounding concession? | https://www.gov.uk/hmrc-internal-manuals/vat-trader-records/vatrec12010 |
| HMRC (UK) | VAT Traders Records Manual VATREC12020 — Rounding at retailers | https://www.gov.uk/hmrc-internal-manuals/vat-trader-records/vatrec12020 |
| HMRC (UK) | VAT Notice 700 paragraph 17.5 — Rounding of total VAT | referenced in VATREC12010; full text at https://www.gov.uk/guidance/vat-guide-notice-700 |
| ATO (Australia) | A New Tax System (Goods and Services Tax) Act 1999, section 9-90 | https://classic.austlii.edu.au/au/legis/cth/consol_act/antsasta1999402/s9.90.html |
| ATO (Australia) | Tax invoices guidance | https://www.ato.gov.au/businesses-and-organisations/gst-excise-and-indirect-taxes/gst/tax-invoices |

Note on VAT Notice 700/45: this notice covers **correcting VAT errors**, not rounding.
The rounding guidance is in Notice 700 §17.5 and the VATREC12000 series of the internal manual.

---

## InvoiceCore rounding summary

- All monetary arithmetic uses `MidpointRounding.AwayFromZero` routed through `Money.Round`.
- Tax is computed by applying the rate to the **rounded subtotal** (sum of rounded line totals),
  not to each line individually.
- Minor-unit precision follows ISO-4217 (0 dp for JPY; 3 dp for KWD; 2 dp for USD, GBP, AUD, EUR, etc.).

---

## HMRC UK VAT

### Rounding rule (VATREC12030)

> "If the VAT on any transaction comes to less than 0.5 of one penny, it should be rounded down.
> If the VAT comes to 0.5 of one penny or more, it should be rounded up."

This is identical to `MidpointRounding.AwayFromZero` for positive amounts. **InvoiceCore matches
the HMRC standard rule exactly.**

### Truncation concession (Notice 700 §17.5, VATREC12010)

> "You **may** round the total VAT payable on all goods and services shown on a VAT invoice
> down to the nearest whole penny."

This concession is permissive, not mandatory. It is available only to invoice traders (not
retailers), and only where the rounding is tax-neutral (the customer recovers the same rounded
amount as input tax). InvoiceCore does **not** implement it. Callers who use the concession must
post-process `TaxAmount`.

### UK 5% reduced rate — 0.5p midpoint boundary

At 5% VAT, whole-penny net values that are multiples of 10p but not 20p produce VAT amounts
with exactly 0.5p in the sub-penny position. This is where the standard rule and the concession
diverge by 1p.

| Net (GBP) | Exact VAT | InvoiceCore tax | HMRC std (half-up) | HMRC concession (floor) | IC vs std | IC vs concession |
|---|---|---|---|---|---|---|
| £0.10 | £0.005 | **£0.01** | £0.01 | £0.00 | match | +£0.01 |
| £0.30 | £0.015 | **£0.02** | £0.02 | £0.01 | match | +£0.01 |
| £0.90 | £0.045 | **£0.05** | £0.05 | £0.04 | match | +£0.01 |
| £3.90 | £0.195 | **£0.20** | £0.20 | £0.19 | match | +£0.01 |
| £1.00 | £0.050 | **£0.05** | £0.05 | £0.05 | match | match |
| £0.23 | £0.0115 | **£0.01** | £0.01 | £0.01 | match | match |

### Why 20% standard rate never hits this boundary

At 20% VAT: for the VAT amount to have exactly 0.5p (£0.005), the net in pence must satisfy
`n × 20 / 100 = x.005`, i.e. `n × 20 ≡ 50 (mod 100)`, i.e. `2n ≡ 5 (mod 10)`. Since 2n is
always even and 5 is odd, this equation has no integer solution. Therefore, on whole-penny
net prices, 20% VAT is **never** exactly at the 0.5p midpoint and the concession can never
differ from the standard rule.

### UK 20% — subtotal-first vs per-line

Three lines at £1.67 each, 20% VAT:

| Method | Subtotal | Tax | Total |
|---|---|---|---|
| InvoiceCore — `Round(£5.01 × 0.20)` | £5.01 | **£1.00** | £6.01 |
| HMRC per-line — `3 × Round(£1.67 × 0.20)` = `3 × £0.33` | £5.01 | £0.99 | £6.00 |

Both methods are permissible under HMRC guidance (VATREC12030). InvoiceCore uses the
subtotal-first method and does not offer a per-line mode. The maximum divergence is 1 minor
unit per invoice; it cannot accumulate beyond that.

### UK 20% — clean cases (no rounding issue)

| Example | Net | IC tax | HMRC std | Match |
|---|---|---|---|---|
| 2 × £50.00, 20% | £100.00 | £20.00 | £20.00 | match |
| 2 × £75.00, 20% | £150.00 | £30.00 | £30.00 | match |
| 1 × £1.50, 20% | £1.50 | £0.30 | £0.30 | match |
| £120.00 gross inclusive 20% | £100.00 net | £20.00 | £20.00 | match |

---

## Australia GST

### Rounding rule (GSTA 1999 s9-90)

> "If the amount of GST on a taxable supply that is the only taxable supply recorded on a
> particular invoice includes a fraction of a cent, the amount of GST is rounded to the nearest
> cent (rounding 0.5 cents upwards)."

This is identical to `MidpointRounding.AwayFromZero` for positive amounts. **InvoiceCore matches
the ATO rule exactly.**

### Two permitted methods

The ATO permits two approaches for invoices with multiple taxable supplies. The buyer and
supplier are not required to use the same method.

**Total-invoice rule:** sum the GST-exclusive values, apply the rate to the total, round once.
InvoiceCore uses this method.

**Taxable-supply rule:** compute GST for each supply separately (rounding each result), then
sum the rounded figures.

### ATO 10% GST — 0.5c midpoint cases

At 10% GST, whole-cent net values that are multiples of 5c but not 10c produce GST with
exactly 0.5c sub-cent.

| Net (AUD) | Exact GST | InvoiceCore GST | ATO rule (half-up) | Match |
|---|---|---|---|---|
| A$0.05 | A$0.005 | **A$0.01** | A$0.01 | match |
| A$0.25 | A$0.025 | **A$0.03** | A$0.03 | match |
| A$0.55 | A$0.055 | **A$0.06** | A$0.06 | match |
| A$1.10 | A$0.110 | **A$0.11** | A$0.11 | match |

### ATO two-method divergence

Two supplies of A$0.05 each, 10% GST:

| Method | Subtotal | GST | Total |
|---|---|---|---|
| InvoiceCore — total-invoice: `Round(A$0.10 × 0.10)` | A$0.10 | **A$0.01** | A$0.11 |
| ATO taxable-supply: `2 × Round(A$0.005)` = `2 × A$0.01` | A$0.10 | A$0.02 | A$0.12 |

InvoiceCore produces A$0.01 less GST than the taxable-supply rule in this case. Both totals
are ATO-compliant; the legislation explicitly permits either method.

---

## Summary of divergences

| Scenario | Divergence | Nature |
|---|---|---|
| HMRC standard rule | None | InvoiceCore matches exactly |
| HMRC optional truncation concession | ±£0.01 at 0.5p midpoints | Concession not implemented; permissive, not required |
| HMRC per-line vs subtotal-first | ±£0.01 per invoice | Architecture choice; both HMRC-permitted |
| ATO s9-90 rule | None | InvoiceCore matches exactly |
| ATO taxable-supply rule vs total-invoice | ±A$0.01 per invoice | InvoiceCore uses total-invoice; both ATO-permitted |

No divergence found where InvoiceCore produces an amount that violates a mandatory rule in
either jurisdiction. All divergences are against optional or alternative methods.
