using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Domain.Enums;
using sales_performance_api.Infrastructure.Persistence;
using sales_performance_api.Infrastructure.Queries;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class AnalyticsDetailsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
	[Fact]
	public async Task Historical_categories_snapshot_names_statuses_and_query_counts_are_correct()
	{
		await using var db = await fixture.CreateFreshContextAsync();
		await db.Database.MigrateAsync();
		var oldCategory = new Category { Name = "Original", SortOrder = 1 };
		var newCategory = new Category { Name = "Current", SortOrder = 2 };
		var product = new Product { Name = "Renamed catalog product", Category = newCategory };
		var manager = new Manager { Name = "Historical Manager", Initials = "HM", IsActive = false };
		var second = new Manager { Name = "Loss Manager", Initials = "LM" };
		var customer = new Customer { Name = "Customer", Company = "Company" };

		Sale SaleAt(int day, SaleStatus status, Manager seller, Category category, decimal price, decimal cost,
			int quantity, string name)
		{
			var sale = new Sale
			{
				SaleDate = new DateTime(2026, 9, day, 12, 0, 0, DateTimeKind.Utc), Status = status, Manager = seller,
				Customer = customer
			};
			sale.Items.Add(new SaleItem
			{
				LineNumber = 1, Product = product, CategoryAtSale = category, ProductNameAtSale = name,
				Quantity = quantity, UnitSalePrice = price, UnitCost = cost
			});
			db.Add(sale);
			return sale;
		}

		SaleAt(1, SaleStatus.Paid, manager, oldCategory, 100, 75, 1, "Old name");
		SaleAt(2, SaleStatus.Paid, manager, oldCategory, 100, 60, 2, "Historical name");
		SaleAt(2, SaleStatus.Paid, second, newCategory, 0, 10, 1, "New snapshot");
		SaleAt(2, SaleStatus.Cancelled, manager, newCategory, 10000, 100, 1, "Cancelled");
		SaleAt(2, SaleStatus.Refunded, manager, newCategory, 10000, 100, 1, "Returned");
		await db.SaveChangesAsync();
		var recorder = new Recorder();
		await using var readDb = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
			.UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(recorder).Options);
		var resolver = new PeriodResolver(new FixedClock(), Options.Create(new ReportingOptions { TimeZone = "UTC" }));
		var details = new AnalyticsDetailsService(resolver, new PostgresAnalyticsReader(readDb),
			new PostgresAnalyticsDetailsReader(readDb));
		var request = new PeriodRequest(PeriodKey.Custom, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 2));
		var categories = (await details.CategoriesAsync(request, default)).Data;
		Assert.Equal(1, recorder.Reads);
		Assert.Equal(["Original", "Current"], categories.Select(x => x.Name));
		Assert.Equal(200, categories[0].Revenue);
		Assert.Equal(80, categories[0].GrossProfit);
		Assert.Equal(-10, categories[1].GrossProfit);
		Assert.Equal(-14.29m, categories[1].GrossProfitShare);
		var products = (await details.ProductsAsync(request, 100, default)).Data;
		Assert.Equal(2, recorder.Reads);
		Assert.Equal(2, products.Count);
		Assert.Equal(product.Id, products[0].ProductId);
		Assert.Equal("Historical name", products[0].Name);
		Assert.Equal(oldCategory.Id, products[0].CategoryId);
		Assert.Equal("New snapshot", products[1].Name);
		Assert.Equal(0, products[1].Margin);
		var ranking = (await details.RankingAsync(request, RankingSort.GrossProfit, default)).Data;
		Assert.Equal(3, recorder.Reads);
		Assert.Equal(manager.Id, ranking[0].Id);
		Assert.Equal("HM", ranking[0].Initials);
		Assert.Equal(220, ranking[0].GrossProfitChange);
		Assert.Equal(100, ranking[0].AvgCheckChange);
		Assert.Null(ranking[1].GrossProfitChange);
		var recent = (await details.RecentAsync(request, 1, 100, default)).Data;
		Assert.Equal(5, recorder.Reads); // One count + one limited page query, regardless of items.
		Assert.Equal(4, recent.TotalCount);
		Assert.All(recent.Items.Where(x => x.Status != "Paid"), x => Assert.Equal(0, x.GrossProfit));
		Assert.Empty(readDb.ChangeTracker.Entries());
	}

	private sealed class FixedClock : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
	}

	private sealed class Recorder : DbCommandInterceptor
	{
		public int Reads { get; private set; }

		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
			CommandEventData eventData,
			InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{
			Reads++;
			return ValueTask.FromResult(result);
		}
	}
}
