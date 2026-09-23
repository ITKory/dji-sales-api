using sales_performance_api.Domain.Enums;

namespace sales_performance_api.Domain.Entities;

public sealed class Sale
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>The instant of the sale; DateTime.Kind must be Utc.</summary>
	public required DateTime SaleDate { get; set; }

	public Guid ManagerId { get; set; }
	public Manager Manager { get; set; } = null!;
	public Guid CustomerId { get; set; }
	public Customer Customer { get; set; } = null!;
	public required SaleStatus Status { get; set; }
	public string Currency { get; set; } = "USD";
	public ICollection<SaleItem> Items { get; } = new List<SaleItem>();
}
