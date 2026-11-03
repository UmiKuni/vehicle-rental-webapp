using CarRental.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Pricing.Data.Configurations;

public sealed class PricingPlanVersionConfiguration : IEntityTypeConfiguration<PricingPlanVersion>
{
    public void Configure(EntityTypeBuilder<PricingPlanVersion> builder)
    {
        builder.ToTable("pricing_plan_versions");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(v => v.VehicleId);

        builder.Property(v => v.Config)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasColumnName("config");

        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");
    }
}
