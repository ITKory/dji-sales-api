using Microsoft.Extensions.Options;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;
using Xunit;

namespace SalesPerformance.UnitTests;

public sealed class DashboardQueryServiceTests
{
	private static readonly DateOnly Today = new(2026, 9, 23);
	private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
	private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");

	[Fact]
	public async Task Average_check_delta_uses_unrounded_checks()
	{
		var reader = new StubReader([
			new(FirstId, "Manager", Today, 1m, 0m, 6),
			new(FirstId, "Manager", Today.AddDays(-1), 1m, 0m, 7)
		]);
		var result = (await Service(reader).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(0.17m, result.AvgCheck);
		Assert.Equal(0.14m, result.AvgCheckPrev);
		Assert.Equal(16.67m, result.AvgCheckDelta); // Rounded inputs would incorrectly produce 21.43%.
	}

	[Fact]
	public async Task Margin_delta_is_unrounded_percentage_point_difference()
	{
		var reader = new StubReader([
			new(FirstId, "Manager", Today, 300m, 299m, 1),
			new(FirstId, "Manager", Today.AddDays(-1), 600m, 599m, 1)
		]);
		var result = (await Service(reader).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(0.33m, result.Margin);
		Assert.Equal(0.17m, result.MarginPrev);
		Assert.Equal(0.17m, result.MarginDelta); // Difference of displayed numbers would be 0.16.
	}

	[Fact]
	public async Task Leader_ties_use_unrounded_average_check_before_id()
	{
		var reader = new StubReader([
			new(FirstId, "Lower check", Today, 0.01m, 0.01m, 4),
			new(SecondId, "Higher check", Today, 0.01m, 0.01m, 3)
		]);
		var result = (await Service(reader).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(SecondId.ToString(), result.BestManagerId);
		Assert.Equal(0m, result.BestManagerValue);
		Assert.Equal(0m, result.BestManagerDelta); // A zero-profit paid manager still exists.
	}

	[Fact]
	public async Task Empty_report_has_zero_values_null_leader_and_daily_zero_arrays()
	{
		var reader = new StubReader([]);
		var report = await Service(reader).GetKpisAsync(new PeriodRequest(PeriodKey.Last7));
		Assert.Equal(0m, report.Data.Revenue);
		Assert.Equal(0m, report.Data.Margin);
		Assert.Equal(0m, report.Data.AvgCheck);
		Assert.Equal(0m, report.Data.RevenueDelta);
		Assert.Null(report.Data.BestManager);
		Assert.Null(report.Data.BestManagerId);
		Assert.Null(report.Data.BestManagerDelta);
		Assert.Equal(6, report.Data.Sparklines.Count);
		Assert.All(report.Data.Sparklines.Values, values =>
		{
			Assert.Equal(7, values.Count);
			Assert.All(values, value => Assert.Equal(0m, value));
		});
	}

	[Fact]
	public async Task Invalid_or_cancelled_requests_never_access_database()
	{
		var reader = new StubReader([]);
		var service = Service(reader);
		await Assert.ThrowsAsync<PeriodValidationException>(() =>
			service.GetKpisAsync(new PeriodRequest(PeriodKey.Custom)));
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.GetKpisAsync(new PeriodRequest(), cancellation.Token));
		Assert.Equal(0, reader.Calls);
	}

	private static DashboardQueryService Service(IAnalyticsReader reader) => new(
		new PeriodResolver(new FixedClock(), Options.Create(new ReportingOptions())), reader);

	private sealed class FixedClock : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
	}

	private sealed class StubReader(IReadOnlyList<ManagerDailyAggregate> rows) : IAnalyticsReader
	{
		public int Calls { get; private set; }

		public Task<IReadOnlyList<ManagerDailyAggregate>> ReadAsync(DateTime startUtc, DateTime endExclusiveUtc,
			string timeZone, CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult(rows);
		}
	}
}
