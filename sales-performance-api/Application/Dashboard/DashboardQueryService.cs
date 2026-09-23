using System.Globalization;
using sales_performance_api.Application.Reporting;
using static sales_performance_api.Application.Reporting.MetricCalculator;

namespace sales_performance_api.Application.Dashboard;

public sealed class DashboardQueryService(IPeriodResolver periods, IAnalyticsReader reader) : IDashboardQueryService
{
	public async Task<Report<KpisResponse>> GetKpisAsync(PeriodRequest request,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var period = periods.Resolve(request);
		var rows = await reader.ReadAsync(period.PreviousStartUtc, period.EndExclusiveUtc, period.TimeZone,
			cancellationToken);
		var current = rows.Where(x => x.Date >= period.From).ToArray();
		var previous = rows.Where(x => x.Date < period.From).ToArray();
		var totals = Total(current);
		var previousTotals = Total(previous);
		var best = current.GroupBy(x => x.ManagerId)
			.Select(g => new { Id = g.Key, Name = g.First().ManagerName, Totals = Total(g) })
			.OrderByDescending(x => x.Totals.GrossProfit)
			.ThenByDescending(x => x.Totals.AvgCheck)
			.ThenBy(x => x.Id.ToString("D"), StringComparer.Ordinal)
			.FirstOrDefault();
		var bestValue = best?.Totals.GrossProfit ?? 0;
		var bestPrevious = best is null ? 0 : Total(previous.Where(x => x.ManagerId == best.Id)).GrossProfit;
		var daily = current.GroupBy(x => x.Date).ToDictionary(g => g.Key, Total);
		var bestDaily = best is null
			? new Dictionary<DateOnly, Totals>()
			: current.Where(x => x.ManagerId == best.Id).ToDictionary(x => x.Date, x => Total([x]));
		var frames = Enumerable.Range(0, period.Days).Select(day =>
		{
			var date = period.From.AddDays(day);
			return (Totals: daily.GetValueOrDefault(date) ?? Totals.Zero,
				BestProfit: bestDaily.GetValueOrDefault(date)?.GrossProfit ?? 0);
		}).ToArray();
		var sparklines = new Dictionary<string, IReadOnlyList<decimal>>
		{
			["revenue"] = frames.Select(x => x.Totals.Revenue).ToArray(),
			["grossProfit"] = frames.Select(x => x.Totals.GrossProfit).ToArray(),
			["margin"] = frames.Select(x => Round(x.Totals.Margin)).ToArray(),
			["salesCount"] = frames.Select(x => (decimal)x.Totals.SalesCount).ToArray(),
			["avgCheck"] = frames.Select(x => Round(x.Totals.AvgCheck)).ToArray(),
			["bestManager"] = frames.Select(x => x.BestProfit).ToArray()
		};
		var result = new KpisResponse(
			totals.Revenue, previousTotals.Revenue, Round(CalculateDelta(totals.Revenue, previousTotals.Revenue)),
			totals.GrossProfit, previousTotals.GrossProfit,
			Round(CalculateDelta(totals.GrossProfit, previousTotals.GrossProfit)),
			Round(totals.Margin), Round(previousTotals.Margin),
			Round(CalculateMarginDelta(totals.Margin, previousTotals.Margin)),
			totals.SalesCount, previousTotals.SalesCount,
			Round(CalculateDelta(totals.SalesCount, previousTotals.SalesCount)),
			Round(totals.AvgCheck), Round(previousTotals.AvgCheck),
			Round(CalculateDelta(totals.AvgCheck, previousTotals.AvgCheck)),
			best?.Id.ToString("D"), best?.Name, bestValue, bestPrevious,
			best is null ? null : Round(CalculateDelta(bestValue, bestPrevious)), sparklines);
		return new Report<KpisResponse>(ReportPeriod.FromResolved(period), "USD", result);
	}

	public async Task<Report<IReadOnlyList<TimeSeriesPointDto>>> GetDynamicsAsync(PeriodRequest request,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var period = periods.Resolve(request);
		var rows = await reader.ReadAsync(period.StartUtc, period.EndExclusiveUtc, period.TimeZone, cancellationToken);
		var daily = rows.GroupBy(x => x.Date).ToDictionary(g => g.Key, Total);
		var points = Enumerable.Range(0, period.Days).Select(day =>
		{
			var date = period.From.AddDays(day);
			var totals = daily.GetValueOrDefault(date) ?? Totals.Zero;
			return new TimeSeriesPointDto(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				totals.Revenue, totals.GrossProfit, totals.SalesCount);
		}).ToArray();
		return new Report<IReadOnlyList<TimeSeriesPointDto>>(ReportPeriod.FromResolved(period), "USD", points);
	}

	private static Totals Total(IEnumerable<ManagerDailyAggregate> rows)
	{
		decimal revenue = 0, cost = 0;
		var count = 0;
		foreach (var row in rows)
		{
			revenue += row.Revenue;
			cost += row.Cost;
			count = checked(count + row.SalesCount);
		}

		return new Totals(revenue, cost, count);
	}

	private sealed record Totals(decimal Revenue, decimal Cost, int SalesCount)
	{
		public static readonly Totals Zero = new(0, 0, 0);
		public decimal GrossProfit => Revenue - Cost;
		public decimal Margin => CalculateMargin(Revenue, GrossProfit);
		public decimal AvgCheck => CalculateAverageCheck(Revenue, SalesCount);
	}
}
