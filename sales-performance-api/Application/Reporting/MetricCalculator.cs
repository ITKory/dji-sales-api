namespace sales_performance_api.Application.Reporting;

public static class MetricCalculator
{
	// Keep ratios unrounded until all derived metrics (including deltas) have been computed.
	public static decimal CalculateMargin(decimal revenue, decimal grossProfit) =>
		revenue == 0 ? 0 : grossProfit / revenue * 100m;

	public static decimal CalculateAverageCheck(decimal revenue, int salesCount) =>
		salesCount == 0 ? 0 : revenue / salesCount;

	public static decimal? CalculateDelta(decimal current, decimal previous) =>
		previous > 0 ? (current - previous) / previous * 100m
		: previous == 0 && current == 0 ? 0m : null;

	public static decimal CalculateMarginDelta(decimal currentMargin, decimal previousMargin) =>
		currentMargin - previousMargin;

	public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
	public static decimal? Round(decimal? value) => value.HasValue ? Round(value.Value) : null;
}
