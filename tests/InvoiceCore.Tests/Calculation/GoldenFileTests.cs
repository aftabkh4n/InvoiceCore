using FluentAssertions;
using Xunit;

namespace InvoiceCore.Tests.Calculation;

public sealed class GoldenFileTests
{
    [Theory]
    [MemberData(nameof(GoldenScenarios.AllScenarios), MemberType = typeof(GoldenScenarios))]
    public void Computes_correctly(object scenarioObj)
    {
        var s = (GoldenScenario)scenarioObj;
        var input = new CalculationInput(
            s.Lines.Select(l => new LineItemInput(l.Qty, l.Price, l.LineDisc)).ToList(),
            s.Rates.Select(r => new TaxRateInput(r.Name, r.Pct)).ToList(),
            s.InvDisc,
            s.TaxMode,
            s.Currency);

        var result = InvoiceCalculator.Compute(input);

        result.LineTotals.Should().Equal(s.LineTotals,
            because: $"[{s.Description}] line totals mismatch");

        result.Subtotal.Should().Be(s.Subtotal,
            because: $"[{s.Description}] Subtotal");

        result.DiscountAmount.Should().Be(s.DiscountAmount,
            because: $"[{s.Description}] DiscountAmount");

        result.TaxAmount.Should().Be(s.TaxAmount,
            because: $"[{s.Description}] TaxAmount");

        result.Total.Should().Be(s.Total,
            because: $"[{s.Description}] Total");

        result.TaxBreakdown.Should().HaveCount(s.TaxBreakdown.Length,
            because: $"[{s.Description}] TaxBreakdown count");

        for (var i = 0; i < s.TaxBreakdown.Length; i++)
        {
            var got = result.TaxBreakdown[i];
            var exp = s.TaxBreakdown[i];
            got.Name.Should().Be(exp.Name,
                because: $"[{s.Description}] breakdown[{i}].Name");
            got.Percentage.Should().Be(exp.Percentage,
                because: $"[{s.Description}] breakdown[{i}].Percentage");
            got.Amount.Should().Be(exp.Amount,
                because: $"[{s.Description}] breakdown[{i}].Amount");
        }
    }
}
