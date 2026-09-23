using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
	public void Configure(EntityTypeBuilder<Product> builder)
	{
		builder.ToTable("products", table =>
			table.HasCheckConstraint("ck_products_name", "btrim(name) <> ''"));
		builder.HasKey(x => x.Id).HasName("pk_products");
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
		builder.Property(x => x.CategoryId).HasColumnName("category_id");
		builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
		builder.HasOne(x => x.Category).WithMany(x => x.Products)
			.HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("fk_products_categories");
		builder.HasIndex(x => x.CategoryId).HasDatabaseName("ix_products_category_id");
	}
}
