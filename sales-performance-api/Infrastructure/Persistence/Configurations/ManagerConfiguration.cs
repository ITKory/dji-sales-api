using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class ManagerConfiguration : IEntityTypeConfiguration<Manager>
{
	public void Configure(EntityTypeBuilder<Manager> builder)
	{
		builder.ToTable("managers", table =>
		{
			table.HasCheckConstraint("ck_managers_name", "btrim(name) <> ''");
			table.HasCheckConstraint("ck_managers_initials", "btrim(initials) <> ''");
		});
		builder.HasKey(x => x.Id).HasName("pk_managers");
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
		builder.Property(x => x.Initials).HasColumnName("initials").HasMaxLength(8).IsRequired();
		builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
	}
}
