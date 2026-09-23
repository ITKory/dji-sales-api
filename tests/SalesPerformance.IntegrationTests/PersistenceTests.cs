using Microsoft.EntityFrameworkCore;
using Npgsql;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Domain.Enums;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class PersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
	[Fact]
	public async Task PostgreSql_schema_contains_analytics_indexes_without_duplicate_fk_indexes()
	{
		await using var db = fixture.CreateContext();
		var indexes = await db.Database.SqlQueryRaw<string>(
			"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync();
		Assert.Contains(indexes, x => x.Contains("(status, sale_date)"));
		Assert.Contains(indexes, x => x.Contains("(manager_id, sale_date)"));
		Assert.Contains(indexes, x => x.Contains("(sale_date DESC, id DESC)"));
		Assert.Contains(indexes, x => x.Contains("UNIQUE INDEX") && x.Contains("(sale_id, line_number)"));
		Assert.DoesNotContain(indexes, x => x.EndsWith("(sale_id)"));
		Assert.DoesNotContain(indexes, x => x.EndsWith("(manager_id)"));

		var foreignKeys = await db.Database.SqlQueryRaw<int>("""
		                                                     SELECT count(*)::integer AS "Value" FROM information_schema.table_constraints
		                                                     WHERE constraint_type = 'FOREIGN KEY' AND table_schema = 'public'
		                                                     """).SingleAsync();
		Assert.Equal(6, foreignKeys);
	}

	[Fact]
	public async Task Sale_cannot_reference_a_nonexistent_manager()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		sale.ManagerId = Guid.NewGuid();
		sale.Manager = null!;
		db.Add(sale);
		var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
		var postgres = Assert.IsType<PostgresException>(error.InnerException);
		Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
		Assert.Equal("fk_sales_managers", postgres.ConstraintName);
	}

	[Theory]
	[InlineData(SaleStatus.Paid)]
	[InlineData(SaleStatus.Cancelled)]
	[InlineData(SaleStatus.Refunded)]
	public async Task Sale_graph_round_trips_with_utc_decimals_status_and_explicit_false(SaleStatus status)
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		sale.Status = status;
		sale.Manager.IsActive = false;
		sale.Items.Single().Product.IsActive = false;
		db.Sales.Add(sale);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		var saved = await db.Sales.Include(x => x.Manager).Include(x => x.Customer)
			.Include(x => x.Items).ThenInclude(x => x.Product)
			.Include(x => x.Items).ThenInclude(x => x.CategoryAtSale)
			.SingleAsync(x => x.Id == sale.Id);
		Assert.Equal(sale.SaleDate, saved.SaleDate);
		Assert.Equal(DateTimeKind.Utc, saved.SaleDate.Kind);
		Assert.Equal(status, saved.Status);
		Assert.Equal("USD", saved.Currency);
		Assert.False(saved.Manager.IsActive);
		Assert.False(saved.Items.Single().Product.IsActive);
		Assert.Equal(123.45m, saved.Items.Single().UnitSalePrice);
		Assert.Equal(80.12m, saved.Items.Single().UnitCost);
		Assert.Equal(saved.Items.Single().CategoryIdAtSale, saved.Items.Single().CategoryAtSale.Id);
		Assert.Equal(sale.CustomerId, saved.CustomerId);

		var storedStatus = await db.Database.SqlQuery<string>(
			$"SELECT status AS \"Value\" FROM sales WHERE id = {sale.Id}").SingleAsync();
		Assert.Equal(status.ToString(), storedStatus);
	}

	[Fact]
	public async Task Product_recategorization_and_rename_preserve_sale_snapshots()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		db.Add(sale);
		await db.SaveChangesAsync();
		var item = sale.Items.Single();
		var historicalCategoryId = item.CategoryIdAtSale;
		item.Product.Category = new Category { Name = Guid.NewGuid().ToString() };
		item.Product.Name = "Renamed product";
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		var saved = await db.SaleItems.Include(x => x.Product).SingleAsync(x => x.Id == item.Id);
		Assert.Equal(historicalCategoryId, saved.CategoryIdAtSale);
		Assert.NotEqual(saved.Product.CategoryId, saved.CategoryIdAtSale);
		Assert.Equal("Original product", saved.ProductNameAtSale);
		Assert.Equal(123.45m, saved.UnitSalePrice);
		Assert.Equal(80.12m, saved.UnitCost);
	}

	[Theory]
	[InlineData("quantity", "ck_sale_items_quantity")]
	[InlineData("lineNumber", "ck_sale_items_line_number")]
	[InlineData("salePrice", "ck_sale_items_unit_sale_price")]
	[InlineData("cost", "ck_sale_items_unit_cost")]
	[InlineData("status", "ck_sales_status")]
	[InlineData("currency", "ck_sales_currency")]
	[InlineData("name", "ck_sale_items_product_name")]
	public async Task Database_rejects_invalid_values(string field, string constraint)
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		var item = sale.Items.Single();
		switch (field)
		{
			case "quantity": item.Quantity = 0; break;
			case "lineNumber": item.LineNumber = 0; break;
			case "salePrice": item.UnitSalePrice = -0.01m; break;
			case "cost": item.UnitCost = -0.01m; break;
			case "status": sale.Status = (SaleStatus)99; break;
			case "currency": sale.Currency = "EUR"; break;
			case "name": item.ProductNameAtSale = "  "; break;
		}

		db.Add(sale);
		var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
		var postgres = Assert.IsType<PostgresException>(error.InnerException);
		Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
		Assert.Equal(constraint, postgres.ConstraintName);
	}

	[Fact]
	public async Task Duplicate_line_numbers_are_rejected_within_one_sale()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		var first = sale.Items.Single();
		sale.Items.Add(new SaleItem
		{
			LineNumber = first.LineNumber, Product = first.Product,
			CategoryAtSale = first.CategoryAtSale, ProductNameAtSale = first.ProductNameAtSale,
			Quantity = 1, UnitSalePrice = 0, UnitCost = 0
		});
		db.Add(sale);
		var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
		var postgres = Assert.IsType<PostgresException>(error.InnerException);
		Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
		Assert.Equal("ux_sale_items_sale_id_line_number", postgres.ConstraintName);
	}

	[Theory]
	[InlineData("managers")]
	[InlineData("customers")]
	[InlineData("products")]
	[InlineData("categories")]
	[InlineData("sales")]
	public async Task Referenced_rows_cannot_be_deleted(string table)
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		db.Add(sale);
		await db.SaveChangesAsync();
		// ExecuteDelete bypasses tracked navigations, exercising the actual database FKs.
		var error = await Assert.ThrowsAsync<PostgresException>(async () =>
		{
			_ = table switch
			{
				"managers" => await db.Managers.Where(x => x.Id == sale.ManagerId).ExecuteDeleteAsync(),
				"customers" => await db.Customers.Where(x => x.Id == sale.CustomerId).ExecuteDeleteAsync(),
				"products" => await db.Products.Where(x => x.Id == sale.Items.Single().ProductId).ExecuteDeleteAsync(),
				"categories" => await db.Categories.Where(x => x.Id == sale.Items.Single().CategoryIdAtSale)
					.ExecuteDeleteAsync(),
				_ => await db.Sales.Where(x => x.Id == sale.Id).ExecuteDeleteAsync()
			};
		});
		Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
	}

	[Fact]
	public async Task Free_and_loss_making_paid_sales_are_valid()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sale = CreateSale();
		sale.Items.Single().UnitSalePrice = 0;
		db.Add(sale);
		await db.SaveChangesAsync();
		var profit = await db.SaleItems.Where(x => x.SaleId == sale.Id)
			.SumAsync(x => x.Quantity * (x.UnitSalePrice - x.UnitCost));
		Assert.Equal(-160.24m, profit);
	}

	[Fact]
	public async Task Analytics_filters_translate_and_do_not_hide_other_statuses_globally()
	{
		await using var db = fixture.CreateContext();
		await using var transaction = await db.Database.BeginTransactionAsync();
		var sales = Enum.GetValues<SaleStatus>().Select(status =>
		{
			var sale = CreateSale();
			sale.Status = status;
			return sale;
		}).ToArray();
		db.AddRange(sales);
		await db.SaveChangesAsync();
		var from = sales[0].SaleDate;
		var to = from.AddDays(1);
		var ids = sales.Select(x => x.Id).ToArray();
		Assert.Equal(3, await db.Sales.CountAsync(x => ids.Contains(x.Id)));
		var revenue = await db.SaleItems
			.Where(x => ids.Contains(x.SaleId) && x.Sale.Status == SaleStatus.Paid
			                                   && x.Sale.SaleDate >= from && x.Sale.SaleDate < to)
			.SumAsync(x => x.Quantity * x.UnitSalePrice);
		Assert.Equal(246.90m, revenue);
	}

	private static Sale CreateSale()
	{
		var category = new Category { Name = Guid.NewGuid().ToString() };
		var product = new Product { Name = "Original product", Category = category };
		var sale = new Sale
		{
			SaleDate = new DateTime(2026, 9, 23, 12, 30, 0, DateTimeKind.Utc),
			Manager = new Manager { Name = "Sarah Chen", Initials = "SC" },
			Customer = new Customer { Name = "John Anderson", Company = "Acme" },
			Status = SaleStatus.Paid
		};
		sale.Items.Add(new SaleItem
		{
			LineNumber = 1, Product = product, CategoryAtSale = category,
			ProductNameAtSale = product.Name, Quantity = 2,
			UnitSalePrice = 123.45m, UnitCost = 80.12m
		});
		return sale;
	}
}
