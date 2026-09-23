using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
	public void Configure(EntityTypeBuilder<Customer> builder)
	{
		builder.ToTable("customers", table =>
		{
			table.HasCheckConstraint("ck_customers_name", "btrim(name) <> ''");
			table.HasCheckConstraint("ck_customers_company", "btrim(company) <> ''");
		});
		builder.HasKey(x => x.Id).HasName("pk_customers");
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
		builder.Property(x => x.Company).HasColumnName("company").HasMaxLength(200).IsRequired();
	}
}
