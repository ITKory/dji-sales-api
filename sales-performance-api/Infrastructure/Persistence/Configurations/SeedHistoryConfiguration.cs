using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_performance_api.Infrastructure.Persistence.Seed;

namespace sales_performance_api.Infrastructure.Persistence.Configurations;

public sealed class SeedHistoryConfiguration : IEntityTypeConfiguration<SeedHistory>
{
	public void Configure(EntityTypeBuilder<SeedHistory> builder)
	{
		builder.ToTable("seed_history");
		builder.HasKey(x => x.Version).HasName("pk_seed_history");
		builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(100);
		builder.Property(x => x.AppliedAt).HasColumnName("applied_at").HasColumnType("timestamp with time zone");
	}
}
