# InvoiceCore: Build Process

Spec-first, phase-gated development with manual mutation testing at every phase
boundary. This document records the actual process and concrete findings, not
the intended process.

---

## Phases

| Phase | Scope | Released |
|-------|-------|----------|
| 0 | Scaffold: solution layout, CI, empty projects | — |
| 1 | Money engine: ISO-4217 precision, pipeline, property tests | — |
| 2 | Public models, status machine, validation | — |
| 3 | Service, CSV/JSON export, summary, presentation | — |
| 4 | Docs, packaging, release workflow | 0.1.0, 0.1.1 |
| 5 | net10.0 target | 0.2.0 |
| 6 | JSON export schema: MoneyFormat.String default, IncludeNulls flag, 4-context source-gen | 0.3.0 |
| 7 | TaxCalculationMethod.PerLine, residual analysis, conformance regression tests | 0.4.0 |
| 8 | Fix: exception message pointed at unpublished document | pending (0.4.1) |

Phases 0–4 each ended with a mutation run before the next phase started.
No new code was written against a test gap that a mutation found.

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

Mutations M1-M7 targeted the five-step calculation pipeline and the `Money`
rounding helper.

**M4: remove `Money.Round` from line totals (step 1)**
Initial kills: **2** (`RoundingTests.Quantity_0_333_rounds_line_total_correctly`,
`PropertyTests.Invariant3_all_decimals_already_rounded`).

`Quantity_0_333` covered the USD (2 dp) case. No existing golden scenario
tested a line-level sub-minor-unit result for JPY (0 dp) or KWD (3 dp), which
are the currencies where rounding at the line level can round to a different
integer. The initial kill count of 2 was below the coverage threshold for
a cross-currency rounding site, so S29, S31, and S32 were added specifically
to close it. After those additions M4 kills **5**: `Quantity_0_333_rounds_line_total_correctly`,
`Invariant3_all_decimals_already_rounded`, S29 (USD line-gross midpoint 3.025),
S31 (USD line-gross sub-precision 0.333), S32 (KWD line-gross sub-precision 0.3333).

M4 was re-run against the current suite (v0.4.0): kills **6**; adds
`PropertyTests.PerLine_Invariant3_all_decimals_already_rounded` (Phase 7 property
test that also exercises unrounded line totals via the PerLine path).

The finding is noted in the record because no existing golden scenario had a
line item whose gross exceeded the currency's minor-unit precision — all
prior tests happened to use prices that were already rounded to currency
precision before multiplication.

**M6: remove `Money.Round` from `DiscountAmount` (step 3)**
Initial kills: **1** (Invariant3 only).

`Invariant3` (property test: all stored decimals are already rounded) caught
the mutation independently, but no scenario-level test existed for a
discount midpoint. A single property-test kill against a cross-currency
rounding site was below threshold, making this a hole.

S30 was added to cover the USD midpoint (0.505 → 0.51, AwayFromZero). After
S30 M6 kills **2**: S30, Invariant3.

S33 and S34 were then added to complete the 3-currency coverage: JPY
(500.5 → 501, AwayFromZero; ToEven gives 500) and KWD (5.0005 → 5.001;
ToEven gives 5.000). After those additions M6 kills **4**: S30, S33, S34,
Invariant3.

---

## Phase 3 mutation findings

Mutations M12-M16 targeted the export layer.

**M12: replace `CultureInfo.InvariantCulture` with `CultureInfo.CurrentCulture`
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

**M13: remove double-quote escaping from `CsvField`**
Kills: **1** (`ExportToCsv_doubles_embedded_quotes`).

**M14: include Cancelled invoices in monetary totals**
Kills: **1** (`Cancelled_excluded_from_monetary_totals_but_counted_in_status`).

**M15: collapse `ByCurrency` to a single cross-currency total**
Kills: **5** (all per-currency `SummaryTests`).

**M16: serialize domain `Invoice` directly instead of `InvoiceDto`**
Kills: **0**.

`Invoice` and `InvoiceDto` have identical public property names and types at
v1. The source-generated context produces the same JSON for both. The
`Source_gen_context_resolves_InvoiceDto_type_info` smoke test names the type
explicitly but does not assert any output shape.

Fix: added `ExportToJson_golden_snapshot`, a committed expected string for a
fixed invoice (deterministic ID, known totals), asserted with
`Should().Be(Expected)`. This catches field renames, additions, and serialiser
option changes that property-level assertions miss.

M16 was re-run against the current suite (v0.4.0): kills **10**
(both golden snapshots, both culture-swap invariant tests, JPY/KWD precision
tests, trailing-zeros, and the currency-precision test). The single golden
snapshot predicted in the original finding was the floor; the string-money
precision tests added in Phase 3 supply the additional kills.

---

## Release pipeline finding

This failure belongs in the record because it is a different failure class
from the mutation findings: no test could have caught it, and no local run
surfaces it.

**Symptom.** Tagging `v0.3.0` while `Directory.Build.props` still read
`<Version>0.2.0</Version>` caused `dotnet pack` to produce
`InvoiceCore.0.2.0.nupkg`. NuGet returned HTTP 409 (version already exists).
The workflow step used `--skip-duplicate`, so the 409 was swallowed and every
step reported success. The Actions run showed green; nothing was published.

**Root cause.** The version in the props file and the git tag were out of
sync. The release workflow had no check for this. `--skip-duplicate` is
correct for idempotent re-runs but masked a real error here.

**Fix.** A "Verify tag matches project version" step was added to
`release.yml` before the `pack` step:

```bash
tag="${GITHUB_REF_NAME#v}"
ver=$(grep -oPm1 '(?<=<Version>)[^<]+' Directory.Build.props)
if [ "$tag" != "$ver" ]; then
  echo "Tag $tag does not match Version $ver in Directory.Build.props"
  exit 1
fi
```

This runs before `dotnet pack`, so a mismatch aborts the workflow before
anything is packed or pushed. Verified at v0.3.0 re-tag and again at
v0.4.0: the step logged `Tag 0.4.0 matches project version 0.4.0` and
continued.

---

## Architecture decisions

**Single `Money.Round` call site.** All rounding goes through one static
method. `TreatWarningsAsErrors=true` and code review catch any drift. An
alternative was a custom `decimal`-wrapping struct; rejected because it adds
allocation overhead and complexity for no observable benefit at this scale.

**Four source-gen JSON contexts.** The export layer has two money modes
(`Number` and `String`) and two null-handling modes (`IncludeNulls` and
`WhenWritingNull`), giving four contexts: `InvoiceJsonContext`,
`InvoiceJsonContextNoNulls`, `InvoiceStringJsonContext`, and
`InvoiceStringJsonContextWithNulls`. An earlier design baked `WriteIndented`
as a fifth axis, producing eight contexts; that packed to 232 KB and was cut
in v0.3.0 (commit `1d424ba`). Indented output is now handled by a
`SerializeIndented<T>(T, JsonTypeInfo<T>)` helper that writes to a
`Utf8JsonWriter` with `Indented = true`, keeping the context count at four
and the package under 200 KB.

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

- Payments, partial payments, payment allocation
- Credit notes (negative quantities rejected)
- Recurring invoices
- Withholding tax, reverse charge, compound tax
- Mixed-rate line items (different VAT rates on lines of the same invoice)
- Currency conversion
- Localisation of labels

None of these are stubbed. Stubs imply a contract; these features have no
contract in v1.
