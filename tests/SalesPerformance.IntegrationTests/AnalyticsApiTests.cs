using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using sales_performance_api.Infrastructure.Persistence.Seed;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class AnalyticsApiFixture : IAsyncLifetime
{
	private readonly PostgresFixture _postgres = new();
	public WebApplicationFactory<Program> Factory { get; private set; } = null!;
	public HttpClient Client { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		await _postgres.InitializeAsync();
		await using var db = await _postgres.CreateFreshContextAsync();
		await db.Database.MigrateAsync();
		await new DemoSeeder(db, NullLogger<DemoSeeder>.Instance).SeedAsync();
		var connectionString = db.Database.GetConnectionString();
		Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseSetting("ConnectionStrings:SalesDatabase", connectionString);
			builder.UseSetting("Reporting:TimeZone", "Europe/Belgrade");
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<TimeProvider>();
				services.AddSingleton<TimeProvider>(new FixedClock());
			});
		});
		Client = Factory.CreateClient();
	}

	public async Task DisposeAsync()
	{
		Client.Dispose();
		await Factory.DisposeAsync();
		await _postgres.DisposeAsync();
	}

	private sealed class FixedClock : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
	}
}

public sealed class AnalyticsApiTests(AnalyticsApiFixture fixture) : IClassFixture<AnalyticsApiFixture>
{
	private const string Range = "from=2025-09-24&to=2026-09-23";

	private async Task<JsonElement> Get(string route)
	{
		using var response = await fixture.Client.GetAsync("/api/analytics/" + route);
		var body = await response.Content.ReadAsStringAsync();
		Assert.True(response.IsSuccessStatusCode, body);
		Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
		return JsonDocument.Parse(body).RootElement.Clone();
	}

	[Fact]
	public async Task All_endpoints_agree_on_paid_totals_and_ranking_is_server_sorted()
	{
		var kpi = await Get("kpi?" + Range);
		Assert.Equal("custom", kpi.GetProperty("period").GetProperty("key").GetString());
		Assert.Equal(365, kpi.GetProperty("period").GetProperty("days").GetInt32());
		var metrics = kpi.GetProperty("data");
		Assert.Equal(3054, metrics.GetProperty("salesCount").GetInt32());
		var dynamics = (await Get("dynamics?" + Range)).GetProperty("data").EnumerateArray().ToArray();
		var categories = (await Get("categories?" + Range)).GetProperty("data").EnumerateArray().ToArray();
		var products = (await Get("products/top?" + Range + "&limit=100")).GetProperty("data").EnumerateArray()
			.ToArray();
		var ranking = (await Get("managers/ranking?" + Range + "&sortBy=GrossProfit")).GetProperty("data")
			.EnumerateArray().ToArray();
		Assert.Equal(365, dynamics.Length);
		Assert.Equal(6, categories.Length);
		Assert.Equal(48, products.Length);
		foreach (var rows in new[] { dynamics, categories, products, ranking })
		{
			Assert.Equal(metrics.GetProperty("revenue").GetDecimal(),
				rows.Sum(x => x.GetProperty("revenue").GetDecimal()));
			Assert.Equal(metrics.GetProperty("grossProfit").GetDecimal(),
				rows.Sum(x => x.GetProperty("grossProfit").GetDecimal()));
		}

		var profitOrder = ranking.Select(x => x.GetProperty("grossProfit").GetDecimal()).ToArray();
		Assert.Equal(profitOrder.OrderDescending(), profitOrder);
		Assert.Equal(metrics.GetProperty("bestManagerId").GetString(), ranking[0].GetProperty("id").GetString());
		foreach (var manager in ranking)
		{
			var series = manager.GetProperty("timeSeries").EnumerateArray().ToArray();
			Assert.Equal(365, series.Length);
			Assert.Equal(manager.GetProperty("salesCount").GetInt32(),
				series.Sum(x => x.GetProperty("salesCount").GetInt32()));
			Assert.Equal(manager.GetProperty("revenue").GetDecimal(),
				series.Sum(x => x.GetProperty("revenue").GetDecimal()));
		}

		var average = (await Get("managers/ranking?" + Range + "&sortBy=AverageCheck")).GetProperty("data")
			.EnumerateArray().ToArray();
		var values = average.Select(x => x.GetProperty("avgCheck").GetDecimal()).ToArray();
		Assert.Equal(values.OrderDescending(), values);
		Assert.All(categories, category => Assert.Equal(
			decimal.Round(
				category.GetProperty("revenue").GetDecimal() / metrics.GetProperty("revenue").GetDecimal() * 100, 2,
				MidpointRounding.AwayFromZero),
			category.GetProperty("revenueShare").GetDecimal()));
	}

	[Fact]
	public async Task Recent_sales_are_paginated_include_all_statuses_and_preserve_original_amount()
	{
		var first = (await Get("sales/recent?" + Range + "&pageSize=100")).GetProperty("data");
		var second = (await Get("sales/recent?" + Range + "&pageSize=100&page=2")).GetProperty("data");
		Assert.Equal(3600, first.GetProperty("totalCount").GetInt64());
		Assert.Equal(36, first.GetProperty("totalPages").GetInt64());
		Assert.Equal(1, first.GetProperty("pageNumber").GetInt32());
		Assert.Equal(2, second.GetProperty("pageNumber").GetInt32());
		var a = first.GetProperty("items").EnumerateArray().ToArray();
		var b = second.GetProperty("items").EnumerateArray().ToArray();
		Assert.Equal(100, a.Length);
		Assert.Empty(a.Select(x => x.GetProperty("id").GetString())
			.Intersect(b.Select(x => x.GetProperty("id").GetString())));
		var dates = a.Concat(b).Select(x => x.GetProperty("date").GetDateTime()).ToArray();
		Assert.Equal(dates.OrderDescending(), dates);
		Assert.All(dates, date => Assert.Equal(DateTimeKind.Utc, date.Kind));
		var nonPaid = a.Where(x => x.GetProperty("status").GetString() != "Paid").ToArray();
		Assert.Contains(nonPaid, x => x.GetProperty("status").GetString() == "Refunded");
		Assert.Contains(nonPaid, x => x.GetProperty("status").GetString() == "Cancelled");
		Assert.All(nonPaid, sale =>
		{
			Assert.Equal(0, sale.GetProperty("grossProfit").GetDecimal());
			Assert.True(sale.GetProperty("amount").GetDecimal() > 0);
		});
		var beyond = (await Get("sales/recent?" + Range + "&page=2147483647&pageSize=100")).GetProperty("data");
		Assert.Empty(beyond.GetProperty("items").EnumerateArray());
		Assert.Equal(3600, beyond.GetProperty("totalCount").GetInt64());
	}

	[Theory]
	[InlineData("today", 1)]
	[InlineData("last7", 7)]
	[InlineData("last30", 30)]
	[InlineData("thisMonth", 23)]
	[InlineData("lastMonth", 31)]
	public async Task Presets_resolve_on_server(string preset, int days)
	{
		var result = await Get("kpi?preset=" + preset);
		Assert.Equal(days, result.GetProperty("period").GetProperty("days").GetInt32());
		Assert.Equal(preset, result.GetProperty("period").GetProperty("key").GetString());
	}

	[Fact]
	public async Task Defaults_aliases_and_empty_period_are_supported()
	{
		Assert.Equal("last30", (await Get("kpi")).GetProperty("period").GetProperty("key").GetString());
		Assert.Equal("last7", (await Get("kpi?period=last7")).GetProperty("period").GetProperty("key").GetString());
		const string empty = "?from=2026-02-10&to=2026-02-16";
		var kpi = (await Get("kpi" + empty)).GetProperty("data");
		Assert.Equal(0, kpi.GetProperty("revenue").GetDecimal());
		Assert.Equal(JsonValueKind.Null, kpi.GetProperty("bestManager").ValueKind);
		foreach (var route in new[] { "managers/ranking", "categories", "products/top" })
			Assert.Empty((await Get(route + empty)).GetProperty("data").EnumerateArray());
		Assert.Equal(7, (await Get("dynamics" + empty)).GetProperty("data").GetArrayLength());
	}

	[Theory]
	[InlineData("kpi?preset=bad", "preset")]
	[InlineData("kpi?preset=0", "preset")]
	[InlineData("kpi?preset=", "preset")]
	[InlineData("kpi?preset=today&preset=last7", "preset")]
	[InlineData("kpi?preset=today&period=today", "preset")]
	[InlineData("kpi?from=2026-09-01", "to")]
	[InlineData("kpi?from=2026-02-30&to=2026-03-01", "from")]
	[InlineData("dynamics?from=2026-9-01&to=2026-09-02", "from")]
	[InlineData("categories?from=2026-09-03&to=2026-09-01", "to")]
	[InlineData("kpi?from=2026-09-01&to=2026-09-24", "to")]
	[InlineData("kpi?from=2024-09-01&to=2026-09-23", "to")]
	[InlineData("kpi?from=1899-01-01&to=1899-01-02", "from")]
	[InlineData("kpi?preset=today&from=2026-09-23&to=2026-09-23", "from")]
	[InlineData("managers/ranking?sortBy=2", "sortBy")]
	[InlineData("products/top?limit=0", "limit")]
	[InlineData("products/top?limit=101", "limit")]
	[InlineData("sales/recent?page=0", "page")]
	[InlineData("sales/recent?pageSize=101", "pageSize")]
	[InlineData("sales/recent?page=2147483648", "page")]
	[InlineData("sales/recent?page=1.5", "page")]
	[InlineData("categories?status=Paid", "status")]
	public async Task Invalid_queries_return_safe_field_errors(string route, string field)
	{
		using var response = await fixture.Client.GetAsync("/api/analytics/" + route);
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
		var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		Assert.Equal("validation_error", problem.GetProperty("code").GetString());
		Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
		Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
	}

	[Fact]
	public async Task Missing_routes_and_wrong_methods_return_problem_details_and_swagger_documents_query()
	{
		using var missing = await fixture.Client.GetAsync("/api/analytics/missing");
		Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
		Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
		using var method = await fixture.Client.PostAsync("/api/analytics/kpi", null);
		Assert.Equal(HttpStatusCode.MethodNotAllowed, method.StatusCode);
		Assert.Contains("GET", method.Content.Headers.Allow);
		var swagger = JsonDocument.Parse(await fixture.Client.GetStringAsync("/swagger/v1/swagger.json")).RootElement;
		var parameters = swagger.GetProperty("paths").GetProperty("/api/analytics/sales/recent").GetProperty("get")
			.GetProperty("parameters");
		Assert.Contains(parameters.EnumerateArray(), x => x.GetProperty("name").GetString() == "pageSize");
	}

	[Theory]
	[InlineData(false, 500, "internal_error")]
	[InlineData(true, 503, "database_unavailable")]
	public async Task Failures_do_not_expose_internal_exception_details(bool database, int status, string code)
	{
		await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
		{
			services.RemoveAll<sales_performance_api.Application.Dashboard.IAnalyticsDetailsReader>();
			services.AddScoped<sales_performance_api.Application.Dashboard.IAnalyticsDetailsReader>(_ =>
				new FailingReader(database));
		}));
		using var client = factory.CreateClient();
		using var response = await client.GetAsync("/api/analytics/categories");
		Assert.Equal(status, (int)response.StatusCode);
		var body = await response.Content.ReadAsStringAsync();
		Assert.DoesNotContain("sensitive", body);
		Assert.Equal(code, JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
	}

	private sealed class FailingReader(bool database)
		: sales_performance_api.Application.Dashboard.IAnalyticsDetailsReader
	{
		public Task<IReadOnlyList<sales_performance_api.Application.Dashboard.CategoryViewModel>> CategoriesAsync(
			sales_performance_api.Application.Reporting.ResolvedPeriod p, CancellationToken ct)
			=> database
				? throw new Npgsql.NpgsqlException("sensitive connection")
				: throw new InvalidOperationException("sensitive SQL");

		public Task<IReadOnlyList<sales_performance_api.Application.Dashboard.ProductViewModel>> ProductsAsync(
			sales_performance_api.Application.Reporting.ResolvedPeriod p, int limit, CancellationToken ct) =>
			throw new NotImplementedException();

		public
			Task<sales_performance_api.Application.Dashboard.Page<
				sales_performance_api.Application.Dashboard.RecentSaleViewModel>> RecentAsync(
				sales_performance_api.Application.Reporting.ResolvedPeriod p, int page, int pageSize,
				CancellationToken ct) => throw new NotImplementedException();
	}
}
