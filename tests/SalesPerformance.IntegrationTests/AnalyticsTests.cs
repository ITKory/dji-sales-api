using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Domain.Enums;
using sales_performance_api.Infrastructure.Persistence;
using sales_performance_api.Infrastructure.Persistence.Seed;
using sales_performance_api.Infrastructure.Queries;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class AnalyticsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
	[Fact]
	public async Task Kpis_previous_leader_and_sparklines_share_one_query_and_correct_paid_cheque_counts()
	{
		var commands = new QueryRecorder();
		await using var db = fixture.CreateContext(commands);
		await using var transaction = await db.Database.BeginTransactionAsync();
		var a = Manager(1);
		a.IsActive = false;
		var b = Manager(2);
		var returnedLater = AddSale(db, a, "2026-08-31T22:00:00Z", SaleStatus.Paid, (2, 100m, 60m), (1, 50m, 20m));
		AddSale(db, b, "2026-09-02T12:00:00Z", SaleStatus.Paid, (1, 70m, 50m));
		AddSale(db, a, "2026-09-03T21:59:59.999999Z", SaleStatus.Paid, (1, 50m, 20m));
		AddSale(db, a, "2026-08-28T22:00:00Z", SaleStatus.Paid, (1, 100m, 80m));
		AddSale(db, b, "2026-08-31T21:59:59.999999Z", SaleStatus.Paid, (1, 400m, 0m));
		AddSale(db, b, "2026-09-02T12:00:00Z", SaleStatus.Cancelled, (1, 900m, 0m));
		AddSale(db, b, "2026-09-03T12:00:00Z", SaleStatus.Refunded, (1, 1000m, 0m));
		AddSale(db, b, "2026-08-30T12:00:00Z", SaleStatus.Refunded, (1, 5000m, 0m));
		AddSale(db, b, "2026-08-28T21:59:59.999999Z", SaleStatus.Paid, (1, 9999m, 0m));
		AddSale(db, b, "2026-09-03T22:00:00Z", SaleStatus.Paid, (1, 9999m, 0m));
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		commands.Enabled = true;
		var request = new PeriodRequest(PeriodKey.Custom, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
		var service = Service(db);
		var report = await service.GetKpisAsync(request);
		var kpi = report.Data;
		Assert.Single(commands.Commands);
		Assert.Equal(3, commands.ParameterCounts.Single());
		Assert.DoesNotContain("Europe/Belgrade", commands.Commands.Single());
		Assert.Empty(db.ChangeTracker.Entries());
		Assert.Equal(new DateOnly(2026, 8, 29), report.Period.PreviousFrom);
		Assert.Equal(370m, kpi.Revenue);
		Assert.Equal(160m, kpi.GrossProfit);
		Assert.Equal(3, kpi.SalesCount); // Not four line items or five units.
		Assert.Equal(43.24m, kpi.Margin);
		Assert.Equal(123.33m, kpi.AvgCheck);
		Assert.Equal(500m, kpi.RevenuePrev);
		Assert.Equal(420m, kpi.GrossProfitPrev);
		Assert.Equal(2, kpi.SalesCountPrev);
		Assert.Equal(84m, kpi.MarginPrev);
		Assert.Equal(250m, kpi.AvgCheckPrev);
		Assert.Equal(-26m, kpi.RevenueDelta);
		Assert.Equal(-61.90m, kpi.GrossProfitDelta);
		Assert.Equal(-40.76m, kpi.MarginDelta);
		Assert.Equal(50m, kpi.SalesCountDelta);
		Assert.Equal(-50.67m, kpi.AvgCheckDelta);
		Assert.Equal(a.Id.ToString(), kpi.BestManagerId);
		Assert.Equal(a.Name, kpi.BestManager);
		Assert.Equal(140m, kpi.BestManagerValue);
		Assert.Equal(20m, kpi.BestManagerPrev); // Same current leader, not the previous leader's 400.
		Assert.Equal(600m, kpi.BestManagerDelta);
		Assert.Equal(new decimal[] { 110, 0, 30 }, kpi.Sparklines["bestManager"]);
		Assert.Equal(new decimal[] { 250, 70, 50 }, kpi.Sparklines["revenue"]);

		commands.Commands.Clear();
		var dynamics = await service.GetDynamicsAsync(request);
		Assert.Single(commands.Commands);
		Assert.Equal(kpi.Revenue, dynamics.Data.Sum(x => x.Revenue));
		Assert.Equal(kpi.GrossProfit, dynamics.Data.Sum(x => x.GrossProfit));
		Assert.Equal(kpi.SalesCount, dynamics.Data.Sum(x => x.SalesCount));
		Assert.Equal(new[] { "2026-09-01", "2026-09-02", "2026-09-03" }, dynamics.Data.Select(x => x.Date));

		commands.Enabled = false;
		await db.Sales.Where(x => x.Id == returnedLater.Id)
			.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SaleStatus.Refunded));
		var afterRefund = (await service.GetKpisAsync(request)).Data;
		Assert.Equal(120m, afterRefund.Revenue);
		Assert.Equal(50m, afterRefund.GrossProfit);
		Assert.Equal(2, afterRefund.SalesCount);
		Assert.Equal(30m, afterRefund.BestManagerValue);
	}

	[Fact]
	public async Task No_current_paid_sales_has_null_leader_even_when_previous_period_had_a_winner()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var manager = Manager(1);
		AddSale(db, manager, "2026-09-23T12:00:00Z", SaleStatus.Cancelled, (1, 100m, 20m));
		AddSale(db, manager, "2026-09-23T12:00:00Z", SaleStatus.Refunded, (1, 200m, 20m));
		AddSale(db, manager, "2026-09-22T12:00:00Z", SaleStatus.Paid, (1, 100m, 20m));
		await db.SaveChangesAsync();
		var kpi = (await Service(db).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(0m, kpi.Revenue);
		Assert.Equal(0m, kpi.GrossProfit);
		Assert.Equal(0m, kpi.Margin);
		Assert.Equal(0m, kpi.AvgCheck);
		Assert.Equal(0, kpi.SalesCount);
		Assert.Equal(-100m, kpi.RevenueDelta);
		Assert.Null(kpi.BestManagerId);
		Assert.Null(kpi.BestManagerDelta);
		Assert.Equal(0m, kpi.BestManagerPrev);
		Assert.All(kpi.Sparklines.Values, values => Assert.Equal(new decimal[] { 0 }, values));
	}

	[Fact]
	public async Task A_free_loss_making_paid_sale_counts_and_can_be_best_manager()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var loser = Manager(1);
		db.Managers.Add(Manager(2)); // No sales: must not beat the loss-making active seller.
		AddSale(db, loser, "2026-09-23T12:00:00Z", SaleStatus.Paid, (1, 0m, 10m));
		await db.SaveChangesAsync();
		var kpi = (await Service(db).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(1, kpi.SalesCount);
		Assert.Equal(0m, kpi.Revenue);
		Assert.Equal(-10m, kpi.GrossProfit);
		Assert.Equal(0m, kpi.Margin);
		Assert.Equal(0m, kpi.AvgCheck);
		Assert.Equal(loser.Id.ToString(), kpi.BestManagerId);
		Assert.Null(kpi.BestManagerDelta);
		Assert.Null(kpi.GrossProfitDelta);
	}

	[Fact]
	public async Task Profit_ties_use_average_check_then_stable_id()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var a = Manager(1);
		var b = Manager(2);
		var c = Manager(3);
		AddSale(db, b, "2026-09-23T12:00:00Z", SaleStatus.Paid, (1, 100m, 80m));
		AddSale(db, a, "2026-09-23T12:00:00Z", SaleStatus.Paid, (1, 100m, 80m));
		AddSale(db, c, "2026-09-23T12:00:00Z", SaleStatus.Paid, (1, 20m, 10m));
		AddSale(db, c, "2026-09-23T12:00:00Z", SaleStatus.Paid, (1, 20m, 10m));
		await db.SaveChangesAsync();
		var kpi = (await Service(db).GetKpisAsync(new PeriodRequest(PeriodKey.Today))).Data;
		Assert.Equal(a.Id.ToString(), kpi.BestManagerId);
		Assert.Equal(20m, kpi.BestManagerValue);
	}

	[Theory]
	[InlineData("2026-03-29", "2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z")]
	[InlineData("2026-10-25", "2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z")]
	public async Task Dst_grouping_uses_reporting_zone_not_database_session_zone(string date, string start, string end)
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		await db.Database.ExecuteSqlRawAsync("SET LOCAL TIME ZONE 'America/Los_Angeles'");
		var manager = Manager(1);
		var startUtc = DateTimeOffset.Parse(start);
		foreach (var hours in new[] { 0m, 2.5m, 3.5m })
			AddSale(db, manager, startUtc.AddHours((double)hours).ToString("O"), SaleStatus.Paid, (1, 10m, 4m));
		AddSale(db, manager, end, SaleStatus.Paid, (1, 999m, 0m));
		AddSale(db, manager, startUtc.AddSeconds(-1).ToString("O"), SaleStatus.Paid, (1, 5m, 1m));
		await db.SaveChangesAsync();
		var request = new PeriodRequest(PeriodKey.Custom, DateOnly.Parse(date), DateOnly.Parse(date));
		var service = Service(db, "2026-11-01T12:00:00Z");
		var kpi = (await service.GetKpisAsync(request)).Data;
		Assert.Equal(30m, kpi.Revenue);
		Assert.Equal(3, kpi.SalesCount);
		Assert.Equal(5m, kpi.RevenuePrev);
		var dynamics = (await service.GetDynamicsAsync(request)).Data;
		Assert.Equal(date, Assert.Single(dynamics).Date);
		Assert.Equal(30m, dynamics.Single().Revenue);
	}

	[Fact]
	public async Task Seed_reports_match_independent_cheque_calculation_for_every_period()
	{
		await using var setup = await fixture.CreateFreshContextAsync();
		await new DatabaseInitializer(setup, new DemoSeeder(setup, NullLogger<DemoSeeder>.Instance),
			NullLogger<DatabaseInitializer>.Instance).InitializeAsync(true);
		var recorder = new QueryRecorder { Enabled = true };
		await using var db = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
			.UseNpgsql(setup.Database.GetConnectionString()).AddInterceptors(recorder).Options);
		var data = DemoDataGenerator.Generate();
		var resolver = Resolver("2026-09-23T12:00:00Z");
		var service = new DashboardQueryService(resolver, new PostgresAnalyticsReader(db));
		foreach (var key in Enum.GetValues<PeriodKey>())
		{
			var request = key == PeriodKey.Custom
				? new PeriodRequest(key, DemoDataGenerator.StartDate, DemoDataGenerator.EndDate)
				: new PeriodRequest(key);
			var range = resolver.Resolve(request);
			recorder.Commands.Clear();
			var report = await service.GetKpisAsync(request);
			Assert.Single(recorder.Commands);
			var current = data.Sales.Where(x => x.Status == SaleStatus.Paid && x.SaleDate >= range.StartUtc
			                                                                && x.SaleDate < range.EndExclusiveUtc)
				.ToArray();
			var previous = data.Sales.Where(x => x.Status == SaleStatus.Paid && x.SaleDate >= range.PreviousStartUtc
			                                                                 && x.SaleDate <
			                                                                 range.PreviousEndExclusiveUtc).ToArray();
			Assert.Equal(current.Sum(s => s.Items.Sum(i => i.Quantity * i.UnitSalePrice)), report.Data.Revenue);
			Assert.Equal(current.Sum(s => s.Items.Sum(i => i.Quantity * (i.UnitSalePrice - i.UnitCost))),
				report.Data.GrossProfit);
			Assert.Equal(current.Length, report.Data.SalesCount);
			Assert.Equal(previous.Sum(s => s.Items.Sum(i => i.Quantity * i.UnitSalePrice)), report.Data.RevenuePrev);
			Assert.Equal(previous.Sum(s => s.Items.Sum(i => i.Quantity * (i.UnitSalePrice - i.UnitCost))),
				report.Data.GrossProfitPrev);
			Assert.Equal(previous.Length, report.Data.SalesCountPrev);
			Assert.Equal(report.Data.Revenue, report.Data.Sparklines["revenue"].Sum());
			Assert.Equal(report.Data.GrossProfit, report.Data.Sparklines["grossProfit"].Sum());
			Assert.Equal(report.Data.SalesCount, report.Data.Sparklines["salesCount"].Sum());
			Assert.All(report.Data.Sparklines.Values, values => Assert.Equal(range.Days, values.Count));
		}

		Assert.Empty(db.ChangeTracker.Entries());
		var quiet = await service.GetDynamicsAsync(new PeriodRequest(PeriodKey.Custom,
			DemoDataGenerator.QuietFrom, DemoDataGenerator.QuietTo));
		Assert.Equal(7, quiet.Data.Count);
		Assert.All(quiet.Data, point =>
		{
			Assert.Equal(0m, point.Revenue);
			Assert.Equal(0, point.SalesCount);
		});
	}

	private static DashboardQueryService Service(SalesDbContext db, string now = "2026-09-23T12:00:00Z") =>
		new(Resolver(now), new PostgresAnalyticsReader(db));

	private static PeriodResolver Resolver(string now) =>
		new(new FixedClock(now), Options.Create(new ReportingOptions()));

	private sealed class FixedClock(string now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse(now);
	}

	private static Manager Manager(int index) => new()
	{
		Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"), Name = $"Manager {index}", Initials = "MM"
	};

	private static Sale AddSale(SalesDbContext db, Manager manager, string utc, SaleStatus status,
		params (int Quantity, decimal Price, decimal Cost)[] lines)
	{
		var sale = new Sale
		{
			Manager = manager, SaleDate = DateTimeOffset.Parse(utc).UtcDateTime, Status = status,
			Customer = new Customer { Name = "Customer", Company = "Company" }
		};
		foreach (var line in lines)
		{
			var category = new Category { Name = Guid.NewGuid().ToString() };
			sale.Items.Add(new SaleItem
			{
				Sale = sale, LineNumber = sale.Items.Count + 1,
				Product = new Product { Name = "Product", Category = category }, CategoryAtSale = category,
				ProductNameAtSale = "Product", Quantity = line.Quantity, UnitSalePrice = line.Price,
				UnitCost = line.Cost
			});
		}

		db.Add(sale);
		return sale;
	}

	private sealed class QueryRecorder : DbCommandInterceptor
	{
		public bool Enabled { get; set; }
		public List<string> Commands { get; } = [];
		public List<int> ParameterCounts { get; } = [];

		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
			CommandEventData eventData, InterceptionResult<DbDataReader> result,
			CancellationToken cancellationToken = default)
		{
			if (Enabled)
			{
				Commands.Add(command.CommandText);
				ParameterCounts.Add(command.Parameters.Count);
			}

			return ValueTask.FromResult(result);
		}
	}
}
