using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace sales_performance_api.Infrastructure.Persistence;

public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
	public SalesDbContext CreateDbContext(string[] args)
	{
		// Model/migration generation does not connect. Database update needs an explicit env value or --connection.
		var connection = Environment.GetEnvironmentVariable("ConnectionStrings__SalesDatabase")
		                 ?? "Host=localhost;Database=sales_performance;Username=sales_performance";
		return new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(connection).Options);
	}
}
