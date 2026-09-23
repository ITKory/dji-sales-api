using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
	public void Configure(EntityTypeBuilder<Sale> builder)
	{
		builder.ToTable("sales", table =>
		{
			table.HasCheckConstraint("ck_sales_status", "status IN ('Paid', 'Cancelled', 'Refunded')");
			table.HasCheckConstraint("ck_sales_currency", "currency = 'USD'");
			table.HasCheckConstraint("ck_sales_sale_date", "isfinite(sale_date)");
		});
		builder.HasKey(x => x.Id).HasName("pk_sales");
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(x => x.SaleDate).HasColumnName("sale_date").HasColumnType("timestamp with time zone");
		builder.Property(x => x.ManagerId).HasColumnName("manager_id");
		builder.Property(x => x.CustomerId).HasColumnName("customer_id");
		builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
		builder.Property(x => x.Currency).HasColumnName("currency").HasColumnType("character(3)")
			.HasDefaultValue("USD").IsRequired();
		builder.HasOne(x => x.Manager).WithMany(x => x.Sales)
			.HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_sales_managers");
		builder.HasOne(x => x.Customer).WithMany(x => x.Sales)
			.HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_sales_customers");

		// Equality on status, followed by a range on the sale date.
		builder.HasIndex(x => new { x.Status, x.SaleDate }).HasDatabaseName("ix_sales_status_sale_date");
		// Supports manager/date filters for every status and also covers the manager FK.
		builder.HasIndex(x => new { x.ManagerId, x.SaleDate }).HasDatabaseName("ix_sales_manager_id_sale_date");
		builder.HasIndex(x => new { x.SaleDate, x.Id }).IsDescending()
			.HasDatabaseName("ix_sales_sale_date_id");
		builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_sales_customer_id");
	}
}
