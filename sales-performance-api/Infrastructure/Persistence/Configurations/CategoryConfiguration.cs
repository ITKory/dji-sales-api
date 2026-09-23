using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
	public void Configure(EntityTypeBuilder<Category> builder)
	{
		builder.ToTable("categories", table =>
			table.HasCheckConstraint("ck_categories_name", "btrim(name) <> ''"));

		builder
			.HasKey(x => x.Id)
			.HasName("pk_categories");

		builder
			.Property(x => x.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder
			.Property(x => x.Name)
			.HasColumnName("name")
			.HasMaxLength(100)
			.IsRequired();

		builder
			.Property(x => x.SortOrder)
			.HasColumnName("sort_order")
			.HasDefaultValue(0);

		builder
			.HasIndex(x => x.Name)
			.IsUnique()
			.HasDatabaseName("ux_categories_name");
	}
}
