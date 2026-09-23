using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
	public void Configure(EntityTypeBuilder<SaleItem> builder)
	{
		builder.ToTable("sale_items", table =>
		{
			table.HasCheckConstraint("ck_sale_items_line_number", "line_number > 0");
			table.HasCheckConstraint("ck_sale_items_quantity", "quantity > 0");
			table.HasCheckConstraint("ck_sale_items_unit_sale_price", "unit_sale_price >= 0");
			table.HasCheckConstraint("ck_sale_items_unit_cost", "unit_cost >= 0");
			table.HasCheckConstraint("ck_sale_items_product_name", "btrim(product_name_at_sale) <> ''");
		});
		builder.HasKey(x => x.Id).HasName("pk_sale_items");
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(x => x.SaleId).HasColumnName("sale_id");
		builder.Property(x => x.LineNumber).HasColumnName("line_number");
		builder.Property(x => x.ProductId).HasColumnName("product_id");
		builder.Property(x => x.CategoryIdAtSale).HasColumnName("category_id_at_sale");
		builder.Property(x => x.ProductNameAtSale).HasColumnName("product_name_at_sale").HasMaxLength(200).IsRequired();
		builder.Property(x => x.Quantity).HasColumnName("quantity");
		builder.Property(x => x.UnitSalePrice).HasColumnName("unit_sale_price").HasPrecision(18, 2);
		builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2);
		builder.HasOne(x => x.Sale).WithMany(x => x.Items)
			.HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_sale_items_sales");
		builder.HasOne(x => x.Product).WithMany(x => x.SaleItems)
			.HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_sale_items_products");
		builder.HasOne(x => x.CategoryAtSale).WithMany(x => x.SaleItems)
			.HasForeignKey(x => x.CategoryIdAtSale).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_sale_items_categories");
		builder.HasIndex(x => new { x.SaleId, x.LineNumber }).IsUnique()
			.HasDatabaseName("ux_sale_items_sale_id_line_number");
		builder.HasIndex(x => x.ProductId).HasDatabaseName("ix_sale_items_product_id");
		builder.HasIndex(x => x.CategoryIdAtSale).HasDatabaseName("ix_sale_items_category_id_at_sale");
	}
}
