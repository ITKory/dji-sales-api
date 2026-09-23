using sales_performance_api.Application.Reporting;
using Xunit;

namespace SalesPerformance.UnitTests;

public sealed class MetricCalculatorTests
{
	[Theory]
	[InlineData(250, 200, 25)]
	[InlineData(0, 0, 0)]
	[InlineData(0, 200, -100)]
	[InlineData(-100, 200, -150)]
	public void Percentage_change_uses_previous_base(decimal current, decimal previous, decimal expected) =>
		Assert.Equal(expected, MetricCalculator.CalculateDelta(current, previous));

	[Theory]
	[InlineData(250, 0)]
	[InlineData(-10, 0)]
	[InlineData(50, -100)]
	[InlineData(-100, -100)]
	public void Undefined_percentage_changes_are_null(decimal current, decimal previous) =>
		Assert.Null(MetricCalculator.CalculateDelta(current, previous));

	[Fact]
	public void Margins_and_checks_handle_zero_and_losses_without_intermediate_rounding()
	{
		Assert.Equal(44m, MetricCalculator.CalculateMargin(250, 110));
		Assert.Equal(125m, MetricCalculator.CalculateAverageCheck(250, 2));
		Assert.Equal(0m, MetricCalculator.CalculateMargin(0, -10));
		Assert.Equal(0m, MetricCalculator.CalculateAverageCheck(0, 0));
		Assert.Equal(-20m, MetricCalculator.CalculateMargin(100, -20));
		Assert.Equal(1m / 6m, MetricCalculator.CalculateAverageCheck(1, 6));
		Assert.Equal(4m, MetricCalculator.CalculateMarginDelta(44, 40));
		Assert.Equal(1.01m, MetricCalculator.Round(1.005m));
		Assert.Equal(-1.01m, MetricCalculator.Round(-1.005m));
	}
}
