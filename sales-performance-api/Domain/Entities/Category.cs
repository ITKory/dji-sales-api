namespace sales_performance_api.Domain.Entities;

public sealed class Category
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }
	public int SortOrder { get; set; }
	public ICollection<Product> Products { get; } = new List<Product>();
	public ICollection<SaleItem> SaleItems { get; } = new List<SaleItem>();
}
