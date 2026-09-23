namespace sales_performance_api.Domain.Entities;

public sealed class SaleItem
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid SaleId { get; set; }
	public Sale Sale { get; set; } = null!;
	public required int LineNumber { get; set; }
	public Guid ProductId { get; set; }
	public Product Product { get; set; } = null!;

	// Historical values must not be recalculated from the current product catalog.
	public Guid CategoryIdAtSale { get; set; }
	public Category CategoryAtSale { get; set; } = null!;
	public required string ProductNameAtSale { get; set; }
	public required int Quantity { get; set; }
	public required decimal UnitSalePrice { get; set; }
	public required decimal UnitCost { get; set; }
}
