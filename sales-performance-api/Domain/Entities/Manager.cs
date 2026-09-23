namespace sales_performance_api.Domain.Entities;

public sealed class Manager
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }
	public required string Initials { get; set; }
	public bool IsActive { get; set; } = true;
	public ICollection<Sale> Sales { get; } = new List<Sale>();
}
