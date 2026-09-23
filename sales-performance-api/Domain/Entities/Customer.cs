namespace sales_performance_api.Domain.Entities;

public sealed class Customer
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }
	public required string Company { get; set; }
	public ICollection<Sale> Sales { get; } = new List<Sale>();
}
