# InvoiceCore — Build Process

Spec-first, phase-gated development with manual mutation testing at every phase
boundary. This document records the actual process and concrete findings, not
the intended process.

---

## Phases

| Phase | Scope | Commits |
|-------|-------|---------|
| 0 | Scaffold: solution layout, CI, empty projects | 1 |
| 1 | Money engine: ISO-4217 precision, pipeline, property tests | 2 |
| 2 | Public models, status machine, validation | 1 |
| 3 | Service, CSV/JSON export, summary, presentation | 1 |
| 4 | Docs, packaging, release workflow | current |

Each phase ended with a mutation run before the next phase started. No new
code was written against a test gap that a mutation found.

---

## Mutation testing protocol

Mutations were applied by hand, one at a time. Each mutation:

1. Applied to `src/InvoiceCore` only.
2. `dotnet test -f net9.0` run immediately.
3. Kill count recorded.
4. Mutation reverted; suite verified green before the next mutation.

A "kill" is a failing test caused by the mutation. A mutation that kills 0
means the behaviour it alters is not exercised by any test.

---

## Phase 1 mutation findings

Mutations M1–M7 targeted the five-step calculation pipeline and the `Money`
rounding helper.

**M4 — remove `Money.Round` from line totals (step 1)**
Kills: **2** (S08, S09).

The existing golden scenarios did not include a line item whose
`Quantity × UnitPrice` produced a sub-minor-unit result in USD (2 decimal
places). S08 and S09 covered it for USD; the gap was that no scenario tested
it for JPY (0 dp) or KWD (3 dp) at the line level. The two kills were enough
to confirm the rounding site was guarded, so no new scenario was added —
but this is noted as a coverage limit.

**M6 — remove `Money.Round` from `DiscountAmount` (step 3)**
Initial kills: **2** (S30, Invariant3).

S30 tested a USD invoice-discount midpoint (0.505 → 0.51). `Invariant3`
(property test: all stored decimals are already rounded) independently caught
it. The kill count was 2.

S33 and S34 were added at Phase 2 to complete the 3-currency discount
midpoint coverage: JPY (500.5 → 501, AwayFromZero; ToEven gives 500) and KWD
(5.0005 → 5.001; ToEven gives 5.000). After those additions M6 kills **4**:
S30, S33, S34, Invariant3.

---

## Phase 3 mutation findings

Mutations M12–M16 targeted the export layer.

**M12 — replace `CultureInfo.InvariantCulture` with `CultureInfo.CurrentCulture`
in CSV decimal formatting**

Initial result: **0 kills** on `ExportToCsv`. The theory tests that ran under
`de-DE` and `tr-TR` used `SimpleInvoice()`, whose totals are all whole numbers
(Subtotal=100, Tax=20, Total=120). `100m.ToString(new CultureInfo("de-DE"))`
produces `"100"`, which is identical to the invariant output. The culture guard
was decorative for `ExportToCsv`.

The existing `ExportLineItemsToCsv_decimals_invariant_under_tr_TR` test used
`Quantity = 1.5m`, which does differ between tr-TR (`"1,5"`) and invariant
(`"1.5"`), so that test killed M12. Total kills before the fix: **1**
(ExportLineItemsToCsv only).

Fix: replaced `SimpleInvoice()` in the ExportToCsv decimal theory with a
direct `Invoice` constructor call using `UnitPrice = 10.50m`, producing
fractional Subtotal/Tax/Total values. After the fix M12 kills **3**.

This is the concrete failure mode: a culture-swap test that uses only
whole-number monetary values does not verify invariant-culture formatting.

**M13 — remove double-quote escaping from `CsvField`**
Kills: **1** (`ExportToCsv_doubles_embedded_quotes`).

**M14 — include Cancelled invoices in monetary totals**
Kills: **1** (`Cancelled_excluded_from_monetary_totals_but_counted_in_status`).

**M15 — collapse `ByCurrency` to a single cross-currency total**
Kills: **5** (all per-currency `SummaryTests`).

**M16 — serialize domain `Invoice` directly instead of `InvoiceDto`**
Kills: **0**.

`Invoice` and `InvoiceDto` have identical public property names and types at
v1. The source-generated context produces the same JSON for both. The
`Source_gen_context_resolves_InvoiceDto_type_info` smoke test names the type
explicitly but does not assert any output shape.

Fix: added `ExportToJson_golden_snapshot` — a committed expected string for a
fixed invoice (deterministic ID, known totals), asserted with
`Should().Be(Expected)`. This catches field renames, additions, and serialiser
option changes that property-level assertions miss. M16 would now kill 1.

---

## Architecture decisions

**Single `Money.Round` call site.** All rounding goes through one static
method. `TreatWarningsAsErrors=true` and code review catch any drift. An
alternative was a custom `decimal`-wrapping struct; rejected because it adds
allocation overhead and complexity for no observable benefit at this scale.

**Two source-gen JSON contexts.** `System.Text.Json` source generation bakes
`WriteIndented` at compile time. Two contexts (`InvoiceJsonContext` and
`InvoiceJsonContextIndented`) allow AOT-safe indented output without using the
`JsonSerializerOptions` overload that triggers IL2026/IL3050 under
`IsAotCompatible=true` + `TreatWarningsAsErrors=true`.

**`InvoiceStatus.Overdue` is derived, never stored.** Storing it would require
a background job or a time-dependent query. `GetEffectiveStatus(TimeProvider?)`
returns Overdue at read time; `TimeProvider` injection keeps it testable. The
enum value exists so callers can `switch` on the full status surface without
knowing the implementation detail.

**`DateOnly` throughout.** Invoice dates have no time component. Using
`DateTime` would silently introduce time-zone ambiguity in overdue detection.
`DateOnly` was available from .NET 6 and is the correct type.

---

## What was not built

From SPEC.md §2:

- Payments, partial payments, payment allocation
- Credit notes (negative quantities rejected)
- Recurring invoices
- Withholding tax, reverse charge, compound tax
- Per-line tax rates
- Currency conversion
- Localisation of labels

None of these are stubbed. Stubs imply a contract; these features have no
contract in v1.
