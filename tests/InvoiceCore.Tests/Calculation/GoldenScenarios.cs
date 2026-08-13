namespace InvoiceCore.Tests.Calculation;

// ---------------------------------------------------------------------------
// Hand-calculated golden values for the 25 theory scenarios.
// ALL arithmetic below was worked by hand against SPEC.md §4.3 and §4.4.
// If the code disagrees with an expected value here, the code is wrong.
// Do not adjust these values to match failing tests.
// ---------------------------------------------------------------------------

internal sealed record TaxExpectation(string Name, decimal Percentage, decimal Amount);

internal sealed record GoldenScenario(
    string Description,
    string Currency,
    TaxMode TaxMode,
    (decimal Qty, decimal Price, decimal LineDisc)[] Lines,
    (string Name, decimal Pct)[] Rates,
    decimal InvDisc,
    decimal[] LineTotals,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    TaxExpectation[] TaxBreakdown);

internal static class GoldenScenarios
{
    // -----------------------------------------------------------------------
    // Helpers — reduce verbosity
    // -----------------------------------------------------------------------
    private static (decimal, decimal, decimal) L(decimal qty, decimal price, decimal disc = 0m)
        => (qty, price, disc);
    private static (string, decimal) R(string name, decimal pct) => (name, pct);
    private static TaxExpectation T(string name, decimal pct, decimal amt) => new(name, pct, amt);

    // -----------------------------------------------------------------------
    // USD (p=2) — Exclusive mode
    // -----------------------------------------------------------------------

    // S01: single line, single rate, no discounts
    // lineGross=100, lineTotal=100
    // Subtotal=100, taxableBase=100
    // Tax VAT 20%: Round(100*0.20,2)=20.00  → Total=120.00
    private static readonly GoldenScenario S01 = new(
        "USD Excl 20% no discounts",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m)],
        [R("VAT", 20m)],
        0m,
        [100.00m],
        100.00m, 0.00m, 20.00m, 120.00m,
        [T("VAT", 20m, 20.00m)]);

    // S02: single line, single rate, 10% invoice discount
    // lineTotal=100, Subtotal=100
    // DiscountAmount=Round(100*0.10,2)=10, taxableBase=90
    // Tax: Round(90*0.20,2)=18 → Total=108
    private static readonly GoldenScenario S02 = new(
        "USD Excl 20% 10% invoice discount",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m)],
        [R("VAT", 20m)],
        10m,
        [100.00m],
        100.00m, 10.00m, 18.00m, 108.00m,
        [T("VAT", 20m, 18.00m)]);

    // S03: single line, single rate, 10% line discount
    // lineGross=100, lineDisc=Round(100*0.10,2)=10, lineTotal=90
    // Subtotal=90, taxableBase=90, Tax=18 → Total=108
    private static readonly GoldenScenario S03 = new(
        "USD Excl 20% 10% line discount",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m, 10m)],
        [R("VAT", 20m)],
        0m,
        [90.00m],
        90.00m, 0.00m, 18.00m, 108.00m,
        [T("VAT", 20m, 18.00m)]);

    // S04: single line, single rate, both 10% line + 10% invoice discount
    // lineTotal=90, Subtotal=90
    // DiscountAmount=Round(90*0.10,2)=9, taxableBase=81
    // Tax=Round(81*0.20,2)=16.20 → Total=97.20
    private static readonly GoldenScenario S04 = new(
        "USD Excl 20% both 10% line and 10% invoice discount",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m, 10m)],
        [R("VAT", 20m)],
        10m,
        [90.00m],
        90.00m, 9.00m, 16.20m, 97.20m,
        [T("VAT", 20m, 16.20m)]);

    // S05: multi-rate (GST 10% + PST 5%), no discounts
    // lineTotal=100, taxableBase=100
    // GST=Round(100*0.10,2)=10, PST=Round(100*0.05,2)=5 → Tax=15, Total=115
    private static readonly GoldenScenario S05 = new(
        "USD Excl 10%+5% no discounts",
        "USD", TaxMode.Exclusive,
        [L(2, 50.00m)],
        [R("GST", 10m), R("PST", 5m)],
        0m,
        [100.00m],
        100.00m, 0.00m, 15.00m, 115.00m,
        [T("GST", 10m, 10.00m), T("PST", 5m, 5.00m)]);

    // S06: multi-rate, 10% invoice discount
    // taxableBase=90
    // GST=Round(90*0.10,2)=9, PST=Round(90*0.05,2)=4.50 → Tax=13.50, Total=103.50
    private static readonly GoldenScenario S06 = new(
        "USD Excl 10%+5% 10% invoice discount",
        "USD", TaxMode.Exclusive,
        [L(2, 50.00m)],
        [R("GST", 10m), R("PST", 5m)],
        10m,
        [100.00m],
        100.00m, 10.00m, 13.50m, 103.50m,
        [T("GST", 10m, 9.00m), T("PST", 5m, 4.50m)]);

    // S07: multi-rate, 10% line + 10% invoice discount
    // lineTotal=90, Subtotal=90, DiscountAmount=9, taxableBase=81
    // GST=Round(81*0.10,2)=8.10, PST=Round(81*0.05,2)=Round(4.05,2)=4.05
    // Tax=12.15, Total=93.15
    private static readonly GoldenScenario S07 = new(
        "USD Excl 10%+5% both 10% line and 10% invoice discount",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m, 10m)],
        [R("GST", 10m), R("PST", 5m)],
        10m,
        [90.00m],
        90.00m, 9.00m, 12.15m, 93.15m,
        [T("GST", 10m, 8.10m), T("PST", 5m, 4.05m)]);

    // -----------------------------------------------------------------------
    // USD (p=2) — Inclusive mode
    // -----------------------------------------------------------------------

    // S08: inclusive 20%, price already includes tax (exact division — no residual)
    // lineTotal=120, rawSubtotal=120, R=0.20
    // Subtotal=Round(120/1.20,2)=Round(100.00,2)=100
    // Tax=Round(100*0.20,2)=20 → residual=120-(100+20)=0 → Total=120
    private static readonly GoldenScenario S08 = new(
        "USD Incl 20% exact inclusive price (no residual)",
        "USD", TaxMode.Inclusive,
        [L(1, 120.00m)],
        [R("VAT", 20m)],
        0m,
        [120.00m],
        100.00m, 0.00m, 20.00m, 120.00m,
        [T("VAT", 20m, 20.00m)]);

    // S09: inclusive 20%, price=100 (non-divisible — verify no residual here)
    // Subtotal=Round(100/1.20,2)=Round(83.3333..,2)=83.33
    // Tax=Round(83.33*0.20,2)=Round(16.666,2)=16.67
    // residual=100-(83.33+16.67)=0 → Total=100
    private static readonly GoldenScenario S09 = new(
        "USD Incl 20% price=100 (83.33 back-calc)",
        "USD", TaxMode.Inclusive,
        [L(1, 100.00m)],
        [R("VAT", 20m)],
        0m,
        [100.00m],
        83.33m, 0.00m, 16.67m, 100.00m,
        [T("VAT", 20m, 16.67m)]);

    // S10: inclusive 5%+5%, reconciliation rule fires (+0.01 residual)
    // rawSubtotal=10, R=0.10
    // Subtotal=Round(10/1.10,2)=Round(9.0909..,2)=9.09
    // Tax_A=Round(9.09*0.05,2)=Round(0.4545,2)=0.45   [3rd decimal=4 < 5]
    // Tax_B=0.45 → TaxAmount=0.90
    // residual=10.00-(9.09+0.90)=+0.01
    // Add 0.01 to Tax_A (tie: first wins) → Tax_A=0.46 → TaxAmount=0.91
    // Total=9.09+0.91=10.00
    private static readonly GoldenScenario S10 = new(
        "USD Incl 5%+5% reconciliation adds +0.01 to first rate",
        "USD", TaxMode.Inclusive,
        [L(1, 10.00m)],
        [R("Tax_A", 5m), R("Tax_B", 5m)],
        0m,
        [10.00m],
        9.09m, 0.00m, 0.91m, 10.00m,
        [T("Tax_A", 5m, 0.46m), T("Tax_B", 5m, 0.45m)]);

    // S11: inclusive 20%, with 10% invoice discount (reconciliation does NOT apply)
    // Subtotal=Round(120/1.20,2)=100
    // DiscountAmount=Round(100*0.10,2)=10, taxableBase=90
    // Tax=Round(90*0.20,2)=18 → Total=108  (≠ rawSubtotal=120, which is correct)
    private static readonly GoldenScenario S11 = new(
        "USD Incl 20% 10% invoice discount (no reconciliation with discount)",
        "USD", TaxMode.Inclusive,
        [L(1, 120.00m)],
        [R("VAT", 20m)],
        10m,
        [120.00m],
        100.00m, 10.00m, 18.00m, 108.00m,
        [T("VAT", 20m, 18.00m)]);

    // -----------------------------------------------------------------------
    // USD — Zero / empty tax
    // -----------------------------------------------------------------------

    // S12: 0% tax rate (still in breakdown, amount zero)
    // Tax=Round(100*0/100,2)=0 → Total=100
    private static readonly GoldenScenario S12 = new(
        "USD Excl 0% tax rate",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m)],
        [R("Tax", 0m)],
        0m,
        [100.00m],
        100.00m, 0.00m, 0.00m, 100.00m,
        [T("Tax", 0m, 0.00m)]);

    // S13: no tax rates at all
    private static readonly GoldenScenario S13 = new(
        "USD Excl no tax rates",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m)],
        [],
        0m,
        [100.00m],
        100.00m, 0.00m, 0.00m, 100.00m,
        []);

    // -----------------------------------------------------------------------
    // USD — Multi-line
    // -----------------------------------------------------------------------

    // S14: two lines, 20% VAT, no discounts
    // line1: 1*100=100, line2: 2*50=100 → rawSubtotal=200
    // Tax=Round(200*0.20,2)=40 → Total=240
    private static readonly GoldenScenario S14 = new(
        "USD Excl 20% two lines no discounts",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m), L(2, 50.00m)],
        [R("VAT", 20m)],
        0m,
        [100.00m, 100.00m],
        200.00m, 0.00m, 40.00m, 240.00m,
        [T("VAT", 20m, 40.00m)]);

    // S15: two lines with different line discounts, 20% VAT
    // line1: 1*100, disc=10% → lineDisc=10, lineTotal=90
    // line2: 2*50=100, disc=20% → lineDisc=Round(100*0.20,2)=20, lineTotal=80
    // rawSubtotal=170, Tax=Round(170*0.20,2)=34 → Total=204
    private static readonly GoldenScenario S15 = new(
        "USD Excl 20% two lines with different line discounts",
        "USD", TaxMode.Exclusive,
        [L(1, 100.00m, 10m), L(2, 50.00m, 20m)],
        [R("VAT", 20m)],
        0m,
        [90.00m, 80.00m],
        170.00m, 0.00m, 34.00m, 204.00m,
        [T("VAT", 20m, 34.00m)]);

    // S16: complex multi-line with line discounts + invoice discount, multi-rate
    // line1: 1*50, disc=10% → lineDisc=Round(50*0.10,2)=5, lineTotal=45
    // line2: 2*25=50, disc=0% → lineTotal=50
    // rawSubtotal=95, invDisc=20%
    // DiscountAmount=Round(95*0.20,2)=19, taxableBase=76
    // VAT=Round(76*0.10,2)=7.60, ST=Round(76*0.05,2)=3.80 → Tax=11.40, Total=87.40
    private static readonly GoldenScenario S16 = new(
        "USD Excl 10%+5% two lines with line discounts and 20% invoice discount",
        "USD", TaxMode.Exclusive,
        [L(1, 50.00m, 10m), L(2, 25.00m)],
        [R("VAT", 10m), R("ST", 5m)],
        20m,
        [45.00m, 50.00m],
        95.00m, 19.00m, 11.40m, 87.40m,
        [T("VAT", 10m, 7.60m), T("ST", 5m, 3.80m)]);

    // -----------------------------------------------------------------------
    // JPY (p=0) — Exclusive
    // -----------------------------------------------------------------------

    // S17: JPY clean round, 10% CT
    // lineTotal=1000, Tax=Round(1000*0.10,0)=Round(100.0,0)=100 → Total=1100
    private static readonly GoldenScenario S17 = new(
        "JPY Excl 10% no discounts",
        "JPY", TaxMode.Exclusive,
        [L(1, 1000m)],
        [R("CT", 10m)],
        0m,
        [1000m],
        1000m, 0m, 100m, 1100m,
        [T("CT", 10m, 100m)]);

    // S18: JPY midpoint — 99.5 must round to 100 (AwayFromZero), not 99 (banker's)
    // lineTotal=995, Tax=Round(995*0.10,0)=Round(99.5,0)=100 → Total=1095
    private static readonly GoldenScenario S18 = new(
        "JPY Excl 10% midpoint AwayFromZero (99.5 → 100, not 99)",
        "JPY", TaxMode.Exclusive,
        [L(1, 995m)],
        [R("CT", 10m)],
        0m,
        [995m],
        995m, 0m, 100m, 1095m,
        [T("CT", 10m, 100m)]);

    // -----------------------------------------------------------------------
    // JPY (p=0) — Inclusive
    // -----------------------------------------------------------------------

    // S19: JPY inclusive 10%, exact division
    // rawSubtotal=1100, Subtotal=Round(1100/1.10,0)=Round(1000.0,0)=1000
    // Tax=Round(1000*0.10,0)=100 → residual=1100-(1000+100)=0 → Total=1100
    private static readonly GoldenScenario S19 = new(
        "JPY Incl 10% exact (no residual)",
        "JPY", TaxMode.Inclusive,
        [L(1, 1100m)],
        [R("CT", 10m)],
        0m,
        [1100m],
        1000m, 0m, 100m, 1100m,
        [T("CT", 10m, 100m)]);

    // S20: JPY inclusive 5%+5%, reconciliation fires (+1)
    // rawSubtotal=1000, R=0.10
    // Subtotal=Round(1000/1.10,0)=Round(909.09..,0)=909
    // Tax_A=Round(909*0.05,0)=Round(45.45,0)=45  [1st decimal=4 < 5]
    // Tax_B=45 → TaxAmount=90
    // residual=1000-(909+90)=+1
    // Add +1 to Tax_A (tie: first) → Tax_A=46 → TaxAmount=91
    // Total=909+91=1000
    private static readonly GoldenScenario S20 = new(
        "JPY Incl 5%+5% reconciliation adds +1 to first rate",
        "JPY", TaxMode.Inclusive,
        [L(1, 1000m)],
        [R("Tax_A", 5m), R("Tax_B", 5m)],
        0m,
        [1000m],
        909m, 0m, 91m, 1000m,
        [T("Tax_A", 5m, 46m), T("Tax_B", 5m, 45m)]);

    // -----------------------------------------------------------------------
    // KWD (p=3) — Exclusive
    // -----------------------------------------------------------------------

    // S21: KWD clean, 5% VAT
    // lineTotal=100.000, Tax=Round(100.000*0.05,3)=5.000 → Total=105.000
    private static readonly GoldenScenario S21 = new(
        "KWD Excl 5% clean",
        "KWD", TaxMode.Exclusive,
        [L(1, 100.000m)],
        [R("VAT", 5m)],
        0m,
        [100.000m],
        100.000m, 0.000m, 5.000m, 105.000m,
        [T("VAT", 5m, 5.000m)]);

    // S22: KWD 3dp line-discount rounding
    // lineGross=3*10.001=30.003
    // lineDisc=Round(30.003*5/100,3)=Round(1.50015,3)=1.500  [4th decimal=1 < 5]
    // lineTotal=Round(30.003,3)-1.500=30.003-1.500=28.503
    // Tax=Round(28.503*0.05,3)=Round(1.42515,3)=1.425  [4th decimal=1 < 5]
    // Total=28.503+1.425=29.928
    private static readonly GoldenScenario S22 = new(
        "KWD Excl 5% 3dp line discount rounding",
        "KWD", TaxMode.Exclusive,
        [L(3, 10.001m, 5m)],
        [R("VAT", 5m)],
        0m,
        [28.503m],
        28.503m, 0.000m, 1.425m, 29.928m,
        [T("VAT", 5m, 1.425m)]);

    // S23: KWD 5%+3%, invoice discount
    // lineTotal=100.000, Subtotal=100.000
    // DiscountAmount=Round(100.000*0.10,3)=10.000, taxableBase=90.000
    // VAT=Round(90.000*0.05,3)=4.500, ST=Round(90.000*0.03,3)=2.700
    // Tax=7.200 → Total=97.200
    private static readonly GoldenScenario S23 = new(
        "KWD Excl 5%+3% with 10% invoice discount",
        "KWD", TaxMode.Exclusive,
        [L(1, 100.000m)],
        [R("VAT", 5m), R("ST", 3m)],
        10m,
        [100.000m],
        100.000m, 10.000m, 7.200m, 97.200m,
        [T("VAT", 5m, 4.500m), T("ST", 3m, 2.700m)]);

    // -----------------------------------------------------------------------
    // KWD (p=3) — Inclusive
    // -----------------------------------------------------------------------

    // S24: KWD inclusive 5%+5%, negative reconciliation (−0.001)
    // rawSubtotal=10.000, R=0.10
    // Subtotal=Round(10.000/1.10,3)=Round(9.090909..,3)=9.091
    // Tax_VAT=Round(9.091*0.05,3)=Round(0.45455,3)=0.455  [4th decimal=5 → round up]
    // Tax_ST =Round(9.091*0.05,3)=0.455 → TaxAmount=0.910
    // residual=10.000-(9.091+0.910)=10.000-10.001=−0.001
    // Add −0.001 to Tax_VAT (tie: first, both 0.455) → Tax_VAT=0.454
    // TaxAmount=0.454+0.455=0.909 → Total=9.091+0.909=10.000
    private static readonly GoldenScenario S24 = new(
        "KWD Incl 5%+5% reconciliation subtracts 0.001 from first rate",
        "KWD", TaxMode.Inclusive,
        [L(1, 10.000m)],
        [R("Tax_VAT", 5m), R("Tax_ST", 5m)],
        0m,
        [10.000m],
        9.091m, 0.000m, 0.909m, 10.000m,
        [T("Tax_VAT", 5m, 0.454m), T("Tax_ST", 5m, 0.455m)]);

    // S25: USD inclusive multi-rate 10%+7%, no residual
    // R=0.17, Subtotal=Round(100/1.17,2)=Round(85.4700854..,2)=85.47
    // VAT=Round(85.47*0.10,2)=Round(8.547,2)=8.55  [3rd decimal=7 ≥ 5]
    // ST =Round(85.47*0.07,2)=Round(5.9829,2)=5.98  [3rd decimal=2 < 5]
    // TaxAmount=14.53 → residual=100-(85.47+14.53)=0 → Total=100.00
    private static readonly GoldenScenario S25 = new(
        "USD Incl 10%+7% no residual",
        "USD", TaxMode.Inclusive,
        [L(1, 100.00m)],
        [R("VAT", 10m), R("ST", 7m)],
        0m,
        [100.00m],
        85.47m, 0.00m, 14.53m, 100.00m,
        [T("VAT", 10m, 8.55m), T("ST", 7m, 5.98m)]);

    // -----------------------------------------------------------------------
    // Midpoint rounding — AwayFromZero vs ToEven distinguishers
    // Each of S26-S30 contains a midpoint value that produces a different
    // result under banker's rounding.  S31-S32 test sub-precision line gross.
    // -----------------------------------------------------------------------

    // S26: USD excl, VAT 5% on 100.10
    // lineTotal=100.10, taxableBase=100.10
    // Tax=Round(100.10*0.05,2)=Round(5.005,2)=5.01  [midpoint → AwayFromZero up]
    // ToEven gives 5.00 (0 is even) — different result
    private static readonly GoldenScenario S26 = new(
        "USD Excl 5% tax midpoint (5.005 → 5.01 AwayFromZero)",
        "USD", TaxMode.Exclusive,
        [L(1, 100.10m)],
        [R("VAT", 5m)],
        0m,
        [100.10m],
        100.10m, 0.00m, 5.01m, 105.11m,
        [T("VAT", 5m, 5.01m)]);

    // S27: JPY excl, 10% on 1005
    // lineTotal=1005, Tax=Round(1005*0.10,0)=Round(100.5,0)=101  [midpoint → AwayFromZero up]
    // ToEven gives 100 (0 is even) — different result
    private static readonly GoldenScenario S27 = new(
        "JPY Excl 10% tax midpoint (100.5 → 101 AwayFromZero)",
        "JPY", TaxMode.Exclusive,
        [L(1, 1005m)],
        [R("CT", 10m)],
        0m,
        [1005m],
        1005m, 0m, 101m, 1106m,
        [T("CT", 10m, 101m)]);

    // S28: KWD excl, 5% on 10.010
    // Tax=Round(10.010*0.05,3)=Round(0.5005,3)=0.501  [midpoint → AwayFromZero up]
    // ToEven gives 0.500 (0 is even) — different result
    private static readonly GoldenScenario S28 = new(
        "KWD Excl 5% tax midpoint (0.5005 → 0.501 AwayFromZero)",
        "KWD", TaxMode.Exclusive,
        [L(1, 10.010m)],
        [R("VAT", 5m)],
        0m,
        [10.010m],
        10.010m, 0.000m, 0.501m, 10.511m,
        [T("VAT", 5m, 0.501m)]);

    // S29: USD excl, qty 2.5 @ 1.21, no tax
    // lineGross=2.5*1.21=3.025 → Round(3.025,2)=3.03  [midpoint → AwayFromZero up]
    // ToEven gives 3.02 (2 is even) — different result
    // Also catches M4: without Round(lineGross,p) lineTotal would be 3.025 ≠ 3.03
    private static readonly GoldenScenario S29 = new(
        "USD Excl line-gross midpoint (3.025 → 3.03 AwayFromZero)",
        "USD", TaxMode.Exclusive,
        [L(2.5m, 1.21m)],
        [],
        0m,
        [3.03m],
        3.03m, 0.00m, 0.00m, 3.03m,
        []);

    // S30: USD excl, qty 1 @ 10.10, invoice discount 5%, no tax
    // lineTotal=10.10, Subtotal=10.10
    // DiscountAmount=Round(10.10*0.05,2)=Round(0.505,2)=0.51  [midpoint → AwayFromZero up]
    // ToEven gives 0.50 (0 is even) — different result
    // Also catches M6: without Round(discountAmount) → discountAmount=0.505, Total=9.595 ≠ 9.59
    private static readonly GoldenScenario S30 = new(
        "USD Excl invoice-discount midpoint (0.505 → 0.51 AwayFromZero)",
        "USD", TaxMode.Exclusive,
        [L(1, 10.10m)],
        [],
        5m,
        [10.10m],
        10.10m, 0.51m, 0.00m, 9.59m,
        []);

    // S31: USD excl, qty 0.333 @ 1.00, no tax
    // lineGross=0.333*1.00=0.333 → Round(0.333,2)=0.33  [3rd decimal=3 < 5, rounds down]
    // Without Round(lineGross,p) lineTotal would be 0.333 ≠ 0.33 — catches M4
    private static readonly GoldenScenario S31 = new(
        "USD Excl line-gross sub-precision (0.333 → 0.33)",
        "USD", TaxMode.Exclusive,
        [L(0.333m, 1.00m)],
        [],
        0m,
        [0.33m],
        0.33m, 0.00m, 0.00m, 0.33m,
        []);

    // S32: KWD excl, qty 0.3333 @ 1.000, no tax (KWD 3dp variant of S31)
    // lineGross=0.3333*1.000=0.3333 → Round(0.3333,3)=0.333  [4th decimal=3 < 5, rounds down]
    // Without Round(lineGross,p) lineTotal would be 0.3333 ≠ 0.333 — catches M4 for 3dp
    private static readonly GoldenScenario S32 = new(
        "KWD Excl line-gross sub-precision (0.3333 → 0.333)",
        "KWD", TaxMode.Exclusive,
        [L(0.3333m, 1.000m)],
        [],
        0m,
        [0.333m],
        0.333m, 0.000m, 0.000m, 0.333m,
        []);

    // S33: JPY excl, qty 1 @ 1001, invoice discount 50%, no tax
    // DiscountAmount=Round(1001*0.50,0)=Round(500.5,0)=501 [midpoint → AwayFromZero up]
    // ToEven gives 500 (500 is even) — different result; catches M6 for 0dp
    private static readonly GoldenScenario S33 = new(
        "JPY Excl invoice-discount midpoint (500.5 → 501 AwayFromZero)",
        "JPY", TaxMode.Exclusive,
        [L(1, 1001m)],
        [],
        50m,
        [1001m],
        1001m, 501m, 0m, 500m,
        []);

    // S34: KWD excl, qty 1 @ 100.010, invoice discount 5%, no tax
    // DiscountAmount=Round(100.010*0.05,3)=Round(5.0005,3)=5.001 [midpoint → AwayFromZero up]
    // ToEven gives 5.000 (0 in position 3 is even) — different result; catches M6 for 3dp
    private static readonly GoldenScenario S34 = new(
        "KWD Excl invoice-discount midpoint (5.0005 → 5.001 AwayFromZero)",
        "KWD", TaxMode.Exclusive,
        [L(1, 100.010m)],
        [],
        5m,
        [100.010m],
        100.010m, 5.001m, 0.000m, 95.009m,
        []);

    // -----------------------------------------------------------------------
    // Public access for [MemberData]
    // -----------------------------------------------------------------------
    public static IEnumerable<object[]> AllScenarios =>
        new[]
        {
            S01, S02, S03, S04, S05, S06, S07, S08, S09, S10,
            S11, S12, S13, S14, S15, S16, S17, S18, S19, S20,
            S21, S22, S23, S24, S25, S26, S27, S28, S29, S30,
            S31, S32, S33, S34,
        }.Select(s => new object[] { s });
}
