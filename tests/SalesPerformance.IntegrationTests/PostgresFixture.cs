using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using sales_performance_api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SalesPerformance.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
		.WithImage("postgres:16.15-alpine")
		.Build();

	public SalesDbContext CreateContext(params IInterceptor[] interceptors) => new(
		new DbContextOptionsBuilder<SalesDbContext>()
			.UseNpgsql(_container.GetConnectionString())
			.AddInterceptors(interceptors)
			.Options);

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		await using var context = CreateContext();
		await context.Database.MigrateAsync();
	}

	public async Task<SalesDbContext> CreateFreshContextAsync()
	{
		var databaseName = "seed_test_" + Guid.NewGuid().ToString("N");
		await using var connection = new NpgsqlConnection(_container.GetConnectionString());
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
		await command.ExecuteNonQueryAsync();
		var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
			{ Database = databaseName };
		return new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
			.UseNpgsql(connectionString.ConnectionString).Options);
	}

	public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
