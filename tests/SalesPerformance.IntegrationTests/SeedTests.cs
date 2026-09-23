using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Domain.Enums;
using sales_performance_api.Infrastructure.Persistence;
using sales_performance_api.Infrastructure.Persistence.Seed;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class SeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
	[Fact]
	public void Generator_is_reproducible_and_culture_independent()
	{
		var previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			var first = Fingerprint(DemoDataGenerator.Generate());
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
			Assert.Equal(first, Fingerprint(DemoDataGenerator.Generate()));
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	[Fact]
	public void Dataset_has_requested_size_seasonality_tiers_and_quiet_periods()
	{
		var data = DemoDataGenerator.Generate();
		Assert.Equal(20, data.Managers.Count);
		Assert.Equal(80, data.Customers.Count);
		Assert.Equal(6, data.Categories.Count);
		Assert.Equal(48, data.Products.Count);
		Assert.Equal(3600, data.Sales.Count);
		Assert.Equal(DemoDataGenerator.StartDate, DateOnly.FromDateTime(data.Sales.Min(x => x.SaleDate)));
		Assert.Equal(DemoDataGenerator.EndDate, DateOnly.FromDateTime(data.Sales.Max(x => x.SaleDate)));
		Assert.Equal(20, data.Sales.Select(x => x.ManagerId).Distinct().Count());
		Assert.Equal(80, data.Sales.Select(x => x.CustomerId).Distinct().Count());
		Assert.Equal(48, data.Sales.SelectMany(x => x.Items).Select(x => x.ProductId).Distinct().Count());
		foreach (var status in Enum.GetValues<SaleStatus>())
			Assert.True(data.Sales.Count(x => x.Status == status) > 100);
		Assert.DoesNotContain(data.Sales, x => DateOnly.FromDateTime(x.SaleDate) >= DemoDataGenerator.QuietFrom
		                                       && DateOnly.FromDateTime(x.SaleDate) <= DemoDataGenerator.QuietTo);
		var weakIds = data.Managers.Skip(15).Select(x => x.Id).ToHashSet();
		Assert.DoesNotContain(data.Sales, x => weakIds.Contains(x.ManagerId) && x.SaleDate.Month is 7 or 8);
		Assert.True(data.Sales.Count(x => x.SaleDate.Month is 11 or 12) >
		            2 * data.Sales.Count(x => x.SaleDate.Month is 7 or 8));

		var managerIndexes = data.Managers.Select((manager, index) => (manager.Id, index))
			.ToDictionary(x => x.Id, x => x.index);
		var tierStats = data.Sales.Where(x => x.Status == SaleStatus.Paid)
			.GroupBy(s => managerIndexes[s.ManagerId] switch { < 5 => 0, < 15 => 1, _ => 2 })
			.OrderBy(g => g.Key).Select(g => new
			{
				Average = g.Average(s => s.Items.Sum(i => i.Quantity * i.UnitSalePrice)),
				Margin = g.Sum(s => s.Items.Sum(i => i.Quantity * (i.UnitSalePrice - i.UnitCost)))
				         / g.Sum(s => s.Items.Sum(i => i.Quantity * i.UnitSalePrice))
			}).ToArray();
		Assert.True(tierStats[0].Average > tierStats[1].Average && tierStats[1].Average > tierStats[2].Average);
		Assert.True(tierStats[0].Margin > tierStats[1].Margin && tierStats[1].Margin > tierStats[2].Margin);
		var items = data.Sales.SelectMany(x => x.Items).ToArray();
		Assert.Equal(items.Length, items.Select(x => x.Id).Distinct().Count());
		Assert.All(data.Sales, sale =>
		{
			Assert.NotEmpty(sale.Items);
			Assert.Equal(DateTimeKind.Utc, sale.SaleDate.Kind);
			Assert.Equal(sale.Items.Count, sale.Items.Select(x => x.LineNumber).Distinct().Count());
		});
		Assert.Contains(items, x => x.Quantity > 1);
		Assert.Contains(items, x => x.UnitSalePrice == 0);
		Assert.Contains(items, x => x.UnitCost > x.UnitSalePrice);
		Assert.All(items, x =>
		{
			Assert.True(x.Quantity > 0 && x.UnitCost >= 0 && x.UnitSalePrice >= 0);
			Assert.Equal(decimal.Round(x.UnitSalePrice, 2), x.UnitSalePrice);
			Assert.Equal(decimal.Round(x.UnitCost, 2), x.UnitCost);
		});
	}

	[Fact]
	public async Task Initializer_migrates_seeds_and_preserves_edits_on_rerun()
	{
		await using var db = await fixture.CreateFreshContextAsync();
		await Initialize(db, false);
		Assert.Empty(await db.Database.GetPendingMigrationsAsync());
		Assert.False(db.Database.HasPendingModelChanges());
		Assert.Empty(await db.Sales.ToListAsync());
		await Initialize(db, true);
		Assert.Equal(3600, await db.Sales.CountAsync());
		Assert.Single(await db.SeedHistory.ToListAsync());
		var manager = await db.Managers.FirstAsync();
		manager.Name = "Edited by user";
		await db.SaveChangesAsync();
		var appliedAt = await db.SeedHistory.Select(x => x.AppliedAt).SingleAsync();
		await Initialize(db, true);
		Assert.Equal(3600, await db.Sales.CountAsync());
		Assert.Equal(appliedAt, await db.SeedHistory.Select(x => x.AppliedAt).SingleAsync());
		Assert.Equal("Edited by user",
			await db.Managers.Where(x => x.Id == manager.Id).Select(x => x.Name).SingleAsync());
	}

	[Fact]
	public async Task Two_fresh_databases_have_identical_business_data()
	{
		await using var first = await fixture.CreateFreshContextAsync();
		await using var second = await fixture.CreateFreshContextAsync();
		await Initialize(first, true);
		await Initialize(second, true);
		Assert.Equal(await DatabaseFingerprint(first), await DatabaseFingerprint(second));
	}

	[Fact]
	public async Task Concurrent_initializers_do_not_duplicate_migrations_or_seed()
	{
		await using var first = await fixture.CreateFreshContextAsync();
		await using var second = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
			.UseNpgsql(first.Database.GetConnectionString()).Options);
		await Task.WhenAll(Initialize(first, true), Initialize(second, true));
		Assert.Equal(3600, await first.Sales.CountAsync());
		Assert.Single(await first.SeedHistory.ToListAsync());
		Assert.Single(await first.Database.GetAppliedMigrationsAsync());
	}

	[Fact]
	public async Task Failure_after_insert_rolls_back_seed_and_retry_succeeds()
	{
		await using var db = await fixture.CreateFreshContextAsync();
		await Initialize(db, false);
		await using (var failing = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
			             .UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options))
		{
			await Assert.ThrowsAsync<InvalidOperationException>(() => Initialize(failing, true));
		}

		Assert.Equal(0, await db.Managers.CountAsync());
		Assert.Equal(0, await db.SaleItems.CountAsync());
		Assert.Equal(0, await db.SeedHistory.CountAsync());
		await Initialize(db, true);
		Assert.Equal(3600, await db.Sales.CountAsync());
	}

	[Fact]
	public async Task Unmarked_nonempty_database_is_not_modified_by_seed()
	{
		await using var db = await fixture.CreateFreshContextAsync();
		await Initialize(db, false);
		db.Managers.Add(new Manager { Name = "Real manager", Initials = "RM" });
		await db.SaveChangesAsync();
		await Assert.ThrowsAsync<InvalidOperationException>(() => Initialize(db, true));
		Assert.Single(await db.Managers.ToListAsync());
		Assert.Equal(0, await db.Sales.CountAsync());
		Assert.Equal(0, await db.SeedHistory.CountAsync());
	}

	private static Task Initialize(SalesDbContext db, bool seed) => new DatabaseInitializer(db,
			new DemoSeeder(db, NullLogger<DemoSeeder>.Instance), NullLogger<DatabaseInitializer>.Instance)
		.InitializeAsync(seed);

	private static string Fingerprint(DemoData data)
	{
		var json = JsonSerializer.Serialize(new
		{
			Managers = data.Managers.Select(x => new { x.Id, x.Name, x.Initials, x.IsActive }),
			Customers = data.Customers.Select(x => new { x.Id, x.Name, x.Company }),
			Categories = data.Categories.Select(x => new { x.Id, x.Name, x.SortOrder }),
			Products = data.Products.Select(x => new { x.Id, x.Name, x.CategoryId, x.IsActive }),
			Sales = data.Sales.Select(x => new { x.Id, x.SaleDate, x.ManagerId, x.CustomerId, x.Status, x.Currency }),
			Items = data.Sales.SelectMany(x => x.Items).Select(x => new
			{
				x.Id, x.SaleId, x.LineNumber, x.ProductId,
				x.CategoryIdAtSale, x.ProductNameAtSale, x.Quantity, x.UnitSalePrice, x.UnitCost
			})
		});
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}

	private static async Task<string> DatabaseFingerprint(SalesDbContext db)
	{
		var hashes = new List<string>();
		// Table names are a fixed internal allowlist, never external input.
#pragma warning disable EF1002 // Only fixed table names below are interpolated; SQL identifiers cannot be parameters.
		foreach (var table in new[] { "managers", "customers", "categories", "products", "sales", "sale_items" })
			hashes.Add(await db.Database.SqlQueryRaw<string>(
					$"SELECT md5(string_agg(to_jsonb(t)::text, ',' ORDER BY id)) AS \"Value\" FROM {table} t")
				.SingleAsync());
#pragma warning restore EF1002
		return string.Join("/", hashes);
	}

	private sealed class FailAfterSave : SaveChangesInterceptor
	{
		public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
			CancellationToken cancellationToken = default) =>
			throw new InvalidOperationException("Simulated seed failure before commit.");
	}
}
