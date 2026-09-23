using Microsoft.EntityFrameworkCore;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Infrastructure.Persistence.Seed;

namespace sales_performance_api.Infrastructure.Persistence;

public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
	public DbSet<Manager> Managers => Set<Manager>();
	public DbSet<Customer> Customers => Set<Customer>();
	public DbSet<Category> Categories => Set<Category>();
	public DbSet<Product> Products => Set<Product>();
	public DbSet<Sale> Sales => Set<Sale>();
	public DbSet<SaleItem> SaleItems => Set<SaleItem>();

	public DbSet<SeedHistory> SeedHistory => Set<SeedHistory>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.HasDefaultSchema("public");
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
	}
}
