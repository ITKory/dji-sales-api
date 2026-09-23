namespace sales_performance_api.Infrastructure.Persistence.Seed;

// Operational metadata, not part of the sales domain.
public sealed class SeedHistory
{
	public required string Version { get; set; }
	public DateTime AppliedAt { get; set; }
}
