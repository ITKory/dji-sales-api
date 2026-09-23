using Microsoft.EntityFrameworkCore;

namespace sales_performance_api.Infrastructure.Persistence.Seed;

public sealed class DemoSeeder(SalesDbContext db, ILogger<DemoSeeder> logger)
{
	public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
	{
		await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
		// Serialize check+insert across concurrent seed processes, even without the initialization CLI.
		await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78234002)", cancellationToken);
		if (await db.SeedHistory.AnyAsync(x => x.Version == DemoDataGenerator.Version, cancellationToken))
		{
			logger.LogInformation("Seed {Version} already applied; existing data preserved.",
				DemoDataGenerator.Version);
			await transaction.CommitAsync(cancellationToken);
			return false;
		}

		if (await db.Managers.AnyAsync(cancellationToken) || await db.Customers.AnyAsync(cancellationToken)
		                                                  || await db.Categories.AnyAsync(cancellationToken) ||
		                                                  await db.Products.AnyAsync(cancellationToken)
		                                                  || await db.Sales.AnyAsync(cancellationToken) ||
		                                                  await db.SaleItems.AnyAsync(cancellationToken)
		                                                  || await db.SeedHistory.AnyAsync(cancellationToken))
		{
			throw new InvalidOperationException("Demo seed requires an empty database or its existing version marker. "
			                                    + "Existing data will not be overwritten. Set Seed__Enabled=false to use this database without demo data.");
		}

		var data = DemoDataGenerator.Generate();
		db.Managers.AddRange(data.Managers);
		db.Customers.AddRange(data.Customers);
		db.Categories.AddRange(data.Categories);
		db.Products.AddRange(data.Products);
		db.Sales.AddRange(data.Sales);
		db.SeedHistory.Add(new SeedHistory { Version = DemoDataGenerator.Version, AppliedAt = DateTime.UtcNow });
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		logger.LogInformation(
			"Seed {Version} applied: {Managers} managers, {Customers} customers, {Products} products, {Sales} sales ({From} to {To}).",
			DemoDataGenerator.Version, data.Managers.Count, data.Customers.Count, data.Products.Count,
			data.Sales.Count, DemoDataGenerator.StartDate, DemoDataGenerator.EndDate);
		return true;
	}
}
