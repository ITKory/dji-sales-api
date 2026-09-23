using Microsoft.EntityFrameworkCore;
using sales_performance_api.Infrastructure.Persistence.Seed;

namespace sales_performance_api.Infrastructure.Persistence;

public sealed class DatabaseInitializer(SalesDbContext db, DemoSeeder seeder, ILogger<DatabaseInitializer> logger)
{
	public async Task InitializeAsync(bool seedEnabled, CancellationToken cancellationToken = default)
	{
		// EF Core 8 has no built-in migration lock. Keep one physical connection open for the session lock.
		await db.Database.OpenConnectionAsync(cancellationToken);
		try
		{
			await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(78234001)", cancellationToken);
			try
			{
				await db.Database.MigrateAsync(cancellationToken);
				logger.LogInformation("Database migrations applied.");
				if (seedEnabled) await seeder.SeedAsync(cancellationToken);
				else logger.LogInformation("Demo seed disabled.");
			}
			finally
			{
				await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(78234001)", CancellationToken.None);
			}
		}
		finally
		{
			await db.Database.CloseConnectionAsync();
		}
	}
}
