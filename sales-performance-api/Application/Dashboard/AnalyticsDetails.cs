using sales_performance_api.Application.Reporting;

namespace sales_performance_api.Application.Dashboard;

public enum RankingSort { GrossProfit, AverageCheck }
public sealed record ManagerRankingViewModel(Guid Id, string Name, string Initials, int SalesCount,
    decimal Revenue, decimal GrossProfit, decimal AvgCheck, decimal Margin,
    decimal? GrossProfitChange, decimal? AvgCheckChange, IReadOnlyList<TimeSeriesPointDto> TimeSeries);
public sealed record CategoryViewModel(Guid Id, string Name, decimal Revenue, decimal GrossProfit,
    decimal RevenueShare, decimal? GrossProfitShare);
public sealed record ProductViewModel(string Id, Guid ProductId, Guid CategoryId, string Name, string Category,
    decimal Revenue, decimal GrossProfit, decimal Margin);
public sealed record RecentSaleViewModel(Guid Id, DateTime Date, string ManagerName, string ManagerInitials,
    string CustomerName, string CustomerCompany, string ProductSummary, int ProductCount,
    string Status, decimal Amount, decimal GrossProfit);
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long TotalCount, long TotalPages);

public interface IAnalyticsDetailsReader
{
	Task<IReadOnlyList<CategoryViewModel>> CategoriesAsync(ResolvedPeriod period, CancellationToken ct);
	Task<IReadOnlyList<ProductViewModel>> ProductsAsync(ResolvedPeriod period, int limit, CancellationToken ct);
	Task<Page<RecentSaleViewModel>> RecentAsync(ResolvedPeriod period, int page, int pageSize, CancellationToken ct);
}

public sealed class AnalyticsDetailsService(
	IPeriodResolver resolver,
	IAnalyticsReader reader,
	IAnalyticsDetailsReader details)
{
	public async Task<Report<IReadOnlyList<ManagerRankingViewModel>>> RankingAsync(PeriodRequest request, RankingSort sort,
		CancellationToken ct)
	{
		if (!Enum.IsDefined(sort)) throw new PeriodValidationException("sortBy", "Use GrossProfit or AverageCheck.");
		var period = resolver.Resolve(request);
		var rows = await reader.ReadAsync(period.PreviousStartUtc, period.EndExclusiveUtc, period.TimeZone, ct);
		var previous = rows.Where(x => x.Date < period.From).GroupBy(x => x.ManagerId)
			.ToDictionary(g => g.Key,
				g => (Revenue: g.Sum(x => x.Revenue), Profit: g.Sum(x => x.Revenue - x.Cost),
					Count: g.Sum(x => x.SalesCount)));
		var managers = rows.Where(x => x.Date >= period.From).GroupBy(x => x.ManagerId).Select(g =>
		{
			var first = g.First();
			var revenue = g.Sum(x => x.Revenue);
			var profit = g.Sum(x => x.Revenue - x.Cost);
			var count = g.Sum(x => x.SalesCount);
			var avg = MetricCalculator.CalculateAverageCheck(revenue, count);
			var prev = previous.GetValueOrDefault(g.Key);
			var days = g.ToDictionary(x => x.Date);
			var series = Enumerable.Range(0, period.Days).Select(i =>
			{
				var date = period.From.AddDays(i);
				var day = days.GetValueOrDefault(date);
				return new TimeSeriesPointDto(
					date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
					day?.Revenue ?? 0, day is null ? 0 : day.Revenue - day.Cost, day?.SalesCount ?? 0);
			}).ToArray();
			return new
			{
				Avg = avg, Dto = new ManagerRankingViewModel(g.Key, first.ManagerName, first.ManagerInitials, count,
					revenue, profit, MetricCalculator.Round(avg),
					MetricCalculator.Round(MetricCalculator.CalculateMargin(revenue, profit)),
					MetricCalculator.Round(MetricCalculator.CalculateDelta(profit, prev.Profit)),
					MetricCalculator.Round(MetricCalculator.CalculateDelta(avg,
						MetricCalculator.CalculateAverageCheck(prev.Revenue, prev.Count))), series)
			};
		}).ToArray();
		var sorted = sort == RankingSort.GrossProfit
			? managers.OrderByDescending(x => x.Dto.GrossProfit).ThenByDescending(x => x.Avg)
			: managers.OrderByDescending(x => x.Avg).ThenByDescending(x => x.Dto.GrossProfit);
		return new Report<IReadOnlyList<ManagerRankingViewModel>>(ReportPeriod.FromResolved(period), "USD",
			sorted.ThenBy(x => x.Dto.Id.ToString("D"), StringComparer.Ordinal).Select(x => x.Dto).ToArray());
	}

	public async Task<Report<IReadOnlyList<CategoryViewModel>>> CategoriesAsync(PeriodRequest request, CancellationToken ct)
	{
		var period = resolver.Resolve(request);
		return new Report<IReadOnlyList<CategoryViewModel>>(ReportPeriod.FromResolved(period), "USD", await details.CategoriesAsync(period, ct));
	}

	public async Task<Report<IReadOnlyList<ProductViewModel>>> ProductsAsync(PeriodRequest request, int limit,
		CancellationToken ct)
	{
		if (limit is < 1 or > 100) throw new PeriodValidationException("limit", "Must be between 1 and 100.");
		var period = resolver.Resolve(request);
		return new Report<IReadOnlyList<ProductViewModel>>(ReportPeriod.FromResolved(period), "USD", await details.ProductsAsync(period, limit, ct));
	}

	public async Task<Report<Page<RecentSaleViewModel>>> RecentAsync(PeriodRequest request, int page, int pageSize,
		CancellationToken ct)
	{
		if (page < 1) throw new PeriodValidationException("page", "Must be at least 1.");
		if (pageSize is < 1 or > 100) throw new PeriodValidationException("pageSize", "Must be between 1 and 100.");
		var period = resolver.Resolve(request);
		return new Report<Page<RecentSaleViewModel>>(ReportPeriod.FromResolved(period), "USD", await details.RecentAsync(period, page, pageSize, ct));
	}
}
