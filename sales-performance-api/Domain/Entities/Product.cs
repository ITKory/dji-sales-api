namespace sales_performance_api.Domain.Entities;

public sealed class Product
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }
	public Guid CategoryId { get; set; }
	public Category Category { get; set; } = null!;
	public bool IsActive { get; set; } = true;
	public ICollection<SaleItem> SaleItems { get; } = new List<SaleItem>();
}
