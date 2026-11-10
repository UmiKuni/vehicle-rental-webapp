using CarRental.Modules.Fleet.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Fleet.Data.Configurations;

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.ToTable("vehicle_models");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.BrandId)
            .IsRequired()
            .HasColumnName("brand_id");

        builder.HasIndex(m => m.BrandId);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");
    }
}
