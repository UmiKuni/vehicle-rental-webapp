using CarRental.Modules.Fleet.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Fleet.Data.Configurations;

public class VehicleBrandConfiguration : IEntityTypeConfiguration<VehicleBrand>
{
    public void Configure(EntityTypeBuilder<VehicleBrand> builder)
    {
        builder.ToTable("vehicle_brands");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.HasIndex(b => b.Name)
            .IsUnique();
    }
}
