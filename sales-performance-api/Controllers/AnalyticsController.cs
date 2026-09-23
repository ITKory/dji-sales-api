using Microsoft.AspNetCore.Mvc;
using sales_performance_api.Api;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;

namespace sales_performance_api.Controllers;

[ApiController]
[Route("api/analytics")]
[Produces("application/json")]
[ProducesResponseType(typeof(ValidationProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 500)]
[ProducesResponseType(typeof(ProblemDetails), 503)]
public sealed class AnalyticsController(IDashboardQueryService dashboard, AnalyticsDetailsService details)
	: ControllerBase
{
	[HttpGet("kpi")]
	public Task<Report<KpisResponse>> Kpi(CancellationToken ct) =>
		dashboard.GetKpisAsync(new AnalyticsQuery(Request.Query).Period(), ct);

	[HttpGet("dynamics")]
	public Task<Report<IReadOnlyList<TimeSeriesPointDto>>> Dynamics(CancellationToken ct) =>
		dashboard.GetDynamicsAsync(new AnalyticsQuery(Request.Query).Period(), ct);

	[HttpGet("managers/ranking")]
	public Task<Report<IReadOnlyList<ManagerRankingViewModel>>> Ranking(CancellationToken ct)
	{
		var query = new AnalyticsQuery(Request.Query, "sortBy");
		var period = query.Period();
		return details.RankingAsync(period, query.Sort(), ct);
	}

	[HttpGet("categories")]
	public Task<Report<IReadOnlyList<CategoryViewModel>>> Categories(CancellationToken ct) =>
		details.CategoriesAsync(new AnalyticsQuery(Request.Query).Period(), ct);

	[HttpGet("products/top")]
	public Task<Report<IReadOnlyList<ProductViewModel>>> Products(CancellationToken ct)
	{
		var query = new AnalyticsQuery(Request.Query, "limit");
		var period = query.Period();
		return details.ProductsAsync(period, query.Integer("limit", 8, 1, 100), ct);
	}

	[HttpGet("sales/recent")]
	public Task<Report<Page<RecentSaleViewModel>>> Recent(CancellationToken ct)
	{
		var query = new AnalyticsQuery(Request.Query, "page", "pageSize");
		var period = query.Period();
		return details.RecentAsync(period, query.Integer("page", 1, 1, int.MaxValue),
			query.Integer("pageSize", 8, 1, 100), ct);
	}
}
